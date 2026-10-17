"""Public relay policy core. Inject secrets; fail closed without verified endpoint ownership."""
import base64, collections, hashlib, hmac, ipaddress, threading, time

TRANSPORTS=("udp","webrtc")
SIGNAL_KINDS=("offer","answer","bye")
MAX_SDP=12000

class PolicyError(Exception):
    pass

class RelayPolicy:
    def __init__(self, relay_ip, rest_secret, clock=time.time, allow_private=False):
        address=ipaddress.ip_address(relay_ip)
        # allow_private exists only for the loopback development directory.
        if address.version != 4 or not (address.is_global or allow_private) or not rest_secret:
            raise PolicyError("Public IPv4 and server-only TURN secret required")
        self.relay_ip, self.secret, self.clock = str(address), rest_secret, clock
        self.lock=threading.RLock()
        self.rates={}
        self.sessions={}
        self.rooms={}
        self.enabled=True

    def rate(self, ip, kind, maximum):
        # Purge old buckets to keep random source addresses from growing memory forever.
        now=self.clock()
        for key, bucket in list(self.rates.items()):
            while bucket and bucket[0] <= now-60: bucket.popleft()
            if not bucket: del self.rates[key]
        key=(str(ipaddress.ip_address(ip)),kind)
        if key not in self.rates and len(self.rates)>=4096: raise PolicyError("Rate limiter capacity reached")
        bucket=self.rates.setdefault(key,collections.deque())
        if len(bucket)>=maximum: raise PolicyError("Request rate exceeded")
        bucket.append(now)

    def issue(self, source_ip, session_id, transport="udp"):
        with self.lock:
            self.reap()
            if not self.enabled: raise PolicyError("Relay usage cutoff active")
            self.rate(source_ip,"issue",6)
            if len(self.sessions)>=32 or not session_id or session_id in self.sessions: raise PolicyError("Session capacity or identity invalid")
            if transport not in TRANSPORTS: raise PolicyError("Unsupported transport")
            expires=int(self.clock()+21600)
            username=f"{expires}:{session_id}"
            password=base64.b64encode(hmac.new(self.secret,username.encode(),hashlib.sha1).digest()).decode()
            self.sessions[session_id]={"ip":source_ip,"expires":self.clock()+60,"maximum_expires":expires,"endpoint":None,
                "transport":transport,"mailbox":collections.deque()}
            credentials={"username":username,"password":password,"expires":expires,"address":self.relay_ip,"port":3478,"lease":60}
            if transport=="webrtc":
                credentials["urls"]=[f"turn:{self.relay_ip}:3478?transport=udp",f"turn:{self.relay_ip}:3478?transport=tcp"]
            return credentials

    def renew(self, session_id):
        with self.lock:
            session=self.session(session_id)
            session['expires']=min(self.clock()+60,session['maximum_expires'])

    def verify_endpoint(self, session_id, observed_ip, observed_port):
        # Called only after an unpredictable, single-use UDP ownership challenge succeeds.
        # HTTPS source addresses never identify a UDP allocation.
        with self.lock:
            session=self.session(session_id)
            if observed_ip!=self.relay_ip or type(observed_port) is not int or not 49160<=observed_port<=49223: raise PolicyError("Endpoint is outside the relay allocation pool")
            endpoint=(observed_ip,observed_port)
            if any(s["endpoint"]==endpoint for k,s in self.sessions.items() if k!=session_id): raise PolicyError("Allocation already belongs to another session")
            session["endpoint"]=endpoint

    def session(self, session_id):
        self.reap()
        session=self.sessions.get(session_id)
        if not self.enabled or session is None: raise PolicyError("Session unavailable or expired")
        return session

    def create(self, session_id, code, owner_key, join_key):
        with self.lock:
            session=self.session(session_id)
            self.rate(session["ip"],"create",3)
            if session["transport"]=="udp" and session["endpoint"] is None: raise PolicyError("Allocation ownership has not been verified")
            if len(self.rooms)>=32 or sum(r["ip"]==session["ip"] for r in self.rooms.values())>=8: raise PolicyError("Room capacity reached")
            if len(code)!=6 or any(c not in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" for c in code) or code in self.rooms or not owner_key or not join_key: raise PolicyError("Invalid room credentials")
            if any(r["owner"]==session_id for r in self.rooms.values()): raise PolicyError("Session already owns a room")
            endpoint=session["endpoint"] if session["transport"]=="udp" else (code,1)
            self.rooms[code]={"owner":session_id,"ip":session["ip"],"endpoint":endpoint,"owner_key":owner_key,"join_key":join_key,"lease":self.clock()+30,"players":1,"reservations":{},
                "transport":session["transport"],"members":set()}

    def reserve(self, session_id, code, reservation_id):
        with self.lock:
            session=self.session(session_id)
            self.rate(session["ip"],"lookup",30)
            room=self.rooms.get(code)
            if room is None: raise PolicyError("Room unavailable")
            if room.get("transport","udp")!=session["transport"]: raise PolicyError("Lobby uses another platform's transport")
            if session["transport"]=="udp" and session["endpoint"] is None: raise PolicyError("Allocation ownership has not been verified")
            if session_id==room["owner"] or any(v["session"]==session_id for v in room["reservations"].values()): raise PolicyError("Duplicate admission")
            if room["players"]+len(room["reservations"])>=4: raise PolicyError("Room full")
            if not reservation_id or reservation_id in room["reservations"]: raise PolicyError("Invalid reservation")
            # Browser peers must finish ICE and DTLS through the relay before Mirage can request admission.
            hold=30 if session["transport"]=="webrtc" else 10
            room["reservations"][reservation_id]={"session":session_id,"expires":self.clock()+hold}
            return {"code":code,"address":room["endpoint"][0],"port":room["endpoint"][1],"joinKey":room["join_key"],"reservation":reservation_id}

    def consume(self, owner_session, code, owner_key, reservation_id):
        with self.lock:
            self.session(owner_session)
            room=self.rooms.get(code)
            if room is None or room["owner"]!=owner_session or not hmac.compare_digest(room["owner_key"],owner_key): raise PolicyError("Only room owner may admit")
            reservation=room["reservations"].pop(reservation_id,None)
            if reservation is None or room["players"]>=4: raise PolicyError("Admission expired or full")
            room["players"]+=1
            room.setdefault("members",set()).add(reservation["session"])
            return reservation["session"]

    def heartbeat(self, owner_session, code, owner_key, players):
        with self.lock:
            self.session(owner_session)
            room=self.rooms.get(code)
            if room is None or room["owner"]!=owner_session or not hmac.compare_digest(room["owner_key"],owner_key): raise PolicyError("Only room owner may renew")
            if type(players) is not int or not 1<=players<=4: raise PolicyError("Invalid occupancy")
            room.update(players=players,lease=self.clock()+30)

    def quickplay(self, owner_session, code, owner_key, enabled):
        with self.lock:
            self.session(owner_session)
            room=self.rooms.get(code)
            if room is None or room["owner"]!=owner_session or not hmac.compare_digest(room["owner_key"],owner_key):
                raise PolicyError("Only room owner may queue")
            room["queue_until"]=self.clock()+12 if enabled else 0
            if enabled and room["players"]==1 and not room["reservations"]:
                for candidate_code,candidate in self.rooms.items():
                    if candidate_code==code: break
                    if candidate.get("transport","udp")!=room.get("transport","udp"): continue
                    if candidate.get("queue_until",0)>self.clock() and candidate["players"]+len(candidate["reservations"])<4:
                        room["queue_until"]=0
                        return candidate_code
            return None

    def signal(self, sender, code, target, kind, sdp):
        # WebRTC offers/answers travel only between a room's owner and its reserved or admitted peers.
        with self.lock:
            session=self.session(sender)
            self.rate(session["ip"],"signal",300)
            room=self.rooms.get(code)
            if room is None or room.get("transport")!="webrtc": raise PolicyError("Room unavailable")
            if kind not in SIGNAL_KINDS or not isinstance(sdp,str) or len(sdp)>MAX_SDP: raise PolicyError("Invalid signal")
            peers={v["session"] for v in room["reservations"].values()}|room["members"]
            if sender==room["owner"]:
                if target not in peers: raise PolicyError("Signal target is not in this room")
            elif sender in peers and target=="host":
                target=room["owner"]
            else: raise PolicyError("Signal sender is not in this room")
            mailbox=self.session(target)["mailbox"]
            if len(mailbox)>=32: raise PolicyError("Signal mailbox full")
            mailbox.append({"from":sender,"room":code,"kind":kind,"sdp":sdp})

    def drain(self, session_id):
        with self.lock:
            session=self.session(session_id)
            self.rate(session["ip"],"signal",300)
            messages=list(session["mailbox"]);session["mailbox"].clear()
            return messages

    def reap(self):
        now=self.clock()
        for key,s in list(self.sessions.items()):
            if s["expires"]<=now: del self.sessions[key]
        for code,r in list(self.rooms.items()):
            if r["lease"]<=now or r["owner"] not in self.sessions: del self.rooms[code]; continue
            for key,v in list(r["reservations"].items()):
                if v["expires"]<=now or v["session"] not in self.sessions: del r["reservations"][key]

    def cutoff(self):
        # Deployment monitor must additionally block relay UDP; disabling issuance alone cannot stop live allocations.
        with self.lock:
            self.enabled=False
            self.rooms.clear()
            self.sessions.clear()
