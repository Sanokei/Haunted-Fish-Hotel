#!/usr/bin/python3
"""Loopback-only directory behind the443 TLS proxy; room peers are verified TURN allocations."""
import concurrent.futures,hmac,json,os,pathlib,secrets,socket,threading,time
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from relay_policy import RelayPolicy,PolicyError
IP='37.27.158.17'
policy=RelayPolicy(IP,pathlib.Path('/etc/haunted-fish/turn.secret').read_text().strip().encode())
auth={};challenge_lock=threading.Lock();challenges={}
probe=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);probe.bind((IP,49160));probe.settimeout(1)
def receive_proofs():
 while True:
  try:payload,endpoint=probe.recvfrom(256)
  except socket.timeout:continue
  if not payload.startswith(b'HFVERIFY:'):continue
  nonce=payload[9:].decode('ascii',errors='ignore')
  with challenge_lock:
   item=challenges.get(nonce)
   if item and item['endpoint']==endpoint and item['expires']>time.monotonic():item['event'].set()
threading.Thread(target=receive_proofs,daemon=True).start()
slots=threading.BoundedSemaphore(32)
class Server(ThreadingHTTPServer):
 daemon_threads=True
 request_queue_size=32
 def process_request(self,request,address):
  if not slots.acquire(blocking=False):request.close();return
  try:super().process_request(request,address)
  except Exception:slots.release();raise
 def process_request_thread(self,request,address):
  try:super().process_request_thread(request,address)
  finally:slots.release()
class Handler(BaseHTTPRequestHandler):
 protocol_version='HTTP/1.0'
 def log_message(self,*args):pass
 def setup(self):
  super().setup();self.connection.settimeout(10)
 def authorized(self):
  header=self.headers.get('Authorization','')
  if not header.startswith('Bearer '):raise PolicyError('Session authentication required')
  token=header[7:]
  for sid,expected in list(auth.items()):
   if sid not in policy.sessions:auth.pop(sid,None);continue
   if hmac.compare_digest(token,expected):return sid
  raise PolicyError('Session expired or invalid')
 @staticmethod
 def public(code,room):
  return {'code':code,'address':room['endpoint'][0],'port':room['endpoint'][1],'capacity':4,'players':room['players']}
 def execute(self,data,ip):
  path=self.path.split('?')[0];method=self.command
  with policy.lock:policy.reap()
  if path=='/health' and method=='GET':return {'status':'ok','protocol':'mirage159-turn-v1'}
  if path=='/sessions' and method=='POST':
   sid=secrets.token_urlsafe(24);token=secrets.token_urlsafe(32);credentials=policy.issue(ip,sid);auth[sid]=token
   return dict(credentials,session=sid,token=token)
  if path=='/sessions/heartbeat' and method=='POST':
   policy.renew(self.authorized());return {'status':'renewed'}
  if path=='/sessions' and method=='DELETE':
   sid=self.authorized()
   with policy.lock:policy.sessions.pop(sid,None);auth.pop(sid,None);policy.reap()
   return {'status':'closed'}
  parts=path.strip('/').split('/')
  if len(parts)==2 and parts[0]=='sessions' and parts[1]=='verify' and method=='POST':
   sid=self.authorized();session=policy.session(sid);address=data.get('address');port=data.get('port')
   if address!=IP or type(port) is not int or not 49161<=port<=49223:raise PolicyError('Invalid allocation endpoint')
   with challenge_lock:
    if len(challenges)>=32:raise PolicyError('Verification capacity reached')
    nonce=secrets.token_urlsafe(24);item={'endpoint':(address,port),'expires':time.monotonic()+7,'event':threading.Event()};challenges[nonce]=item
   try:
    for _ in range(3):
     probe.sendto(('HFVERIFY:'+nonce).encode(),(address,port))
     if item['event'].wait(2):policy.verify_endpoint(sid,address,port);return {'status':'verified'}
    raise PolicyError('Allocation ownership verification timed out')
   finally:
    with challenge_lock:challenges.pop(nonce,None)
  if path=='/rooms' and method=='POST':
   sid=self.authorized();code=''.join(secrets.choice('ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789') for _ in range(6));owner=secrets.token_urlsafe(32);join=secrets.token_urlsafe(24)
   policy.create(sid,code,owner,join)
   return dict(self.public(code,policy.rooms[code]),ownerKey=owner,joinKey=join)
  if len(parts)<2 or parts[0]!='rooms' or len(parts[1])!=6:raise PolicyError('Unknown endpoint')
  code=parts[1].upper();room=policy.rooms.get(code)
  if room is None:raise PolicyError('Lobby unavailable')
  if len(parts)==2 and method=='GET':
   with policy.lock:policy.rate(ip,'lookup',30)
   if room['players']+len(room['reservations'])>=4:raise PolicyError('Lobby full')
   return self.public(code,room)
  sid=self.authorized()
  if len(parts)==3 and parts[2]=='join' and method=='POST':return policy.reserve(sid,code,secrets.token_urlsafe(24))
  if len(parts)==3 and parts[2]=='admit' and method=='POST':
   admitted=policy.consume(sid,code,str(data.get('ownerKey','')),str(data.get('reservation','')))
   return {'status':'admitted','session':admitted}
  if len(parts)==3 and parts[2]=='heartbeat' and method=='POST':
   owner=str(data.get('ownerKey',''))
   policy.heartbeat(sid,code,owner,data.get('players'))
   matched=policy.quickplay(sid,code,owner,data.get('quickplay') is True)
   if matched:return {'code':matched,'status':'matched'}
   return self.public(code,room)
  if len(parts)==2 and method=='DELETE':
   if sid!=room['owner'] or not hmac.compare_digest(str(data.get('ownerKey','')),room['owner_key']):raise PolicyError('Only owner may close')
   with policy.lock:policy.rooms.pop(code,None)
   return {'status':'closed'}
  raise PolicyError('Unsupported endpoint')
 def handle_request(self):
  try:
   if self.client_address[0]!='127.0.0.1':raise PolicyError('Trusted TLS proxy required')
   import ipaddress
   ip=str(ipaddress.ip_address(self.headers.get('X-Real-IP','')))
   if pathlib.Path('/var/lib/haunted-fish-traffic/cutoff-active').exists():raise PolicyError('Traffic cutoff active')
   length=int(self.headers.get('Content-Length','0'))
   if length<0 or length>4096 or self.headers.get('Transfer-Encoding'):raise PolicyError('Request too large')
   data=json.loads(self.rfile.read(length)) if length else {}
   if not isinstance(data,dict):raise PolicyError('JSON object required')
   result=self.execute(data,ip);status=200
  except PolicyError as error:
   message=str(error)
   if message=='Request rate exceeded':
    result={'error':'Relay rate limit reached. Wait one minute before retrying.'};status=429
   elif message in ('Session capacity or identity invalid','Rate limiter capacity reached','Verification capacity reached'):
    result={'error':'Relay capacity reached. Try again in one minute.'};status=503
   else:
    result={'error':'Unable to complete room request'};status=400
  except (ValueError,TypeError,KeyError):result={'error':'Invalid room request'};status=400
  body=json.dumps(result).encode();self.send_response(status);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.send_header('Cache-Control','no-store');
  if status in (429,503):self.send_header('Retry-After','65')
  self.end_headers();self.wfile.write(body)
 do_GET=do_POST=do_DELETE=handle_request
if __name__=='__main__':Server(('127.0.0.1',8765),Handler).serve_forever()
