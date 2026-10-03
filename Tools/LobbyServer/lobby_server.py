"""LAN development room directory. No relay or NAT traversal."""
import argparse, json, re, secrets, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

class RoomError(Exception):
    def __init__(self, status, message):
        self.status, self.message = status, message

class Directory:
    def __init__(self, ttl=30, clock=time.monotonic):
        self.ttl, self.clock, self.rooms = ttl, clock, {}
        self.lock = threading.Lock()

    @staticmethod
    def public(room):
        return {k: room[k] for k in ("code","address","port","capacity","players","joinKey")}

    def execute(self, method, path, data):
        with self.lock:
            for code in list(self.rooms):
                if self.rooms[code]["expires"] <= self.clock():
                    del self.rooms[code]
            if path == "/health" and method == "GET":
                return {"status":"ok"}
            if path == "/rooms" and method == "POST":
                address, port, capacity = data.get("address"), data.get("port"), data.get("capacity")
                if not isinstance(address,str) or not address or len(address)>253 or any(c.isspace() for c in address):
                    raise RoomError(400,"A reachable host address is required.")
                if type(port) is not int or not 1 <= port <= 65535:
                    raise RoomError(400,"Invalid game port.")
                if type(capacity) is not int or not 2 <= capacity <= 16:
                    raise RoomError(400,"Capacity must be 2 to 16.")
                if len(self.rooms)>=1000:
                    raise RoomError(503,"Directory is full. Try later.")
                code = "".join(secrets.choice("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") for _ in range(6))
                while code in self.rooms:
                    code = "".join(secrets.choice("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") for _ in range(6))
                room = dict(code=code,address=address,port=port,capacity=capacity,players=1,
                    joinKey=secrets.token_urlsafe(24),ownerKey=secrets.token_urlsafe(32),expires=self.clock()+self.ttl)
                self.rooms[code]=room
                return dict(self.public(room),ownerKey=room["ownerKey"])
            parts=path.strip("/").split("/")
            if len(parts) not in (2,3) or parts[0]!="rooms":
                raise RoomError(404,"Unknown endpoint.")
            code=parts[1].strip().upper()
            if not re.fullmatch(r"[A-Z0-9]{6}",code):
                raise RoomError(400,"Enter exactly six letters or digits.")
            room=self.rooms.get(code)
            if room is None:
                raise RoomError(404,"Lobby not found or expired.")
            if method=="GET" and len(parts)==2:
                if room["players"]>=room["capacity"]:
                    raise RoomError(409,"Lobby is full.")
                return self.public(room)
            if not secrets.compare_digest(str(data.get("ownerKey","")),room["ownerKey"]):
                raise RoomError(403,"Only the host can change this lobby.")
            if method=="POST" and len(parts)==3 and parts[2]=="heartbeat":
                count=data.get("players")
                if type(count) is not int or not 1 <= count <= room["capacity"]:
                    raise RoomError(400,"Invalid player count.")
                room.update(players=count,expires=self.clock()+self.ttl)
                return self.public(room)
            if method=="DELETE" and len(parts)==2:
                del self.rooms[code]
                return {"status":"closed"}
            raise RoomError(405,"Unsupported method.")

class Handler(BaseHTTPRequestHandler):
    directory=Directory()
    def handle_request(self):
        try:
            size=int(self.headers.get("Content-Length",0))
            if not 0 <= size <= 4096:
                raise RoomError(413,"Request too large.")
            data=json.loads(self.rfile.read(size)) if size else {}
            if not isinstance(data,dict):
                raise RoomError(400,"JSON object required.")
            result,status=self.directory.execute(self.command,self.path,data),200
        except RoomError as error:
            result,status={"error":error.message},error.status
        except (ValueError,TypeError):
            result,status={"error":"Invalid JSON."},400
        body=json.dumps(result).encode()
        self.send_response(status)
        self.send_header("Content-Type","application/json")
        self.send_header("Content-Length",str(len(body)))
        self.send_header("Cache-Control","no-store")
        self.end_headers()
        self.wfile.write(body)
    do_GET=do_POST=do_DELETE=handle_request
    def log_message(self,format,*args):
        pass

if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bind",default="127.0.0.1")
    parser.add_argument("--port",type=int,default=8787)
    args=parser.parse_args()
    print(f"Lobby directory: http://{args.bind}:{args.port}",flush=True)
    ThreadingHTTPServer((args.bind,args.port),Handler).serve_forever()

