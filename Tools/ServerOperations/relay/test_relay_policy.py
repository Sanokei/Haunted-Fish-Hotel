import concurrent.futures, unittest
from relay_policy import RelayPolicy, PolicyError
class PolicyTests(unittest.TestCase):
 def setUp(self):
  self.now=1000
  self.p=RelayPolicy("8.8.8.8",b"fixed-test-only-not-deployment-secret",lambda:self.now)
 def issue(self,n):
  self.p.issue(f"198.51.100.{n}",str(n));self.p.verify_endpoint(str(n),"8.8.8.8",49160+n)
 def room(self):
  self.issue(1);self.p.create("1","ABC123","owner-test","join-test")
 def test_no_secret_fails(self):
  with self.assertRaises(PolicyError): RelayPolicy("8.8.8.8",b"")
 def test_no_unverified_host(self):
  self.p.issue("198.51.100.1","1")
  with self.assertRaises(PolicyError): self.p.create("1","ABC123","owner-test","join-test")
 def test_peer_restriction(self):
  self.p.issue("198.51.100.1","1")
  with self.assertRaises(PolicyError): self.p.verify_endpoint("1","127.0.0.1",49161)
  with self.assertRaises(PolicyError): self.p.verify_endpoint("1","8.8.8.8",7777)
 def test_atomic_capacity(self):
  self.room()
  for n in range(2,10):self.issue(n)
  def reserve(n):
   try:self.p.reserve(str(n),"ABC123",str(n));return True
   except PolicyError:return False
  with concurrent.futures.ThreadPoolExecutor(8) as pool: results=list(pool.map(reserve,range(2,10)))
  self.assertEqual(sum(results),3)
 def test_expiry(self):
  self.room();self.issue(2);self.p.reserve("2","ABC123","ticket")
  self.now+=11
  with self.assertRaises(PolicyError):self.p.consume("1","ABC123","owner-test","ticket")
  self.now+=20;self.p.reap();self.assertFalse(self.p.rooms)
 def test_owner_and_single_use(self):
  self.room();self.issue(2);self.p.reserve("2","ABC123","ticket")
  with self.assertRaises(PolicyError): self.p.consume("2","ABC123","owner-test","ticket")
  self.assertEqual(self.p.consume("1","ABC123","owner-test","ticket"),"2")
  with self.assertRaises(PolicyError):self.p.consume("1","ABC123","owner-test","ticket")
 def test_rate_limit(self):
  for n in range(6):self.p.issue("198.51.100.1",str(n))
  with self.assertRaises(PolicyError):self.p.issue("198.51.100.1","excess")
 def test_abandoned_session_releases_slot(self):
  self.p.issue("198.51.100.1","abandoned")
  self.now+=61;self.p.reap()
  self.assertNotIn("abandoned",self.p.sessions)
 def test_heartbeat_extends_only_live_session(self):
  self.p.issue("198.51.100.1","live")
  self.now+=40;self.p.renew("live")
  self.now+=40;self.assertIsNotNone(self.p.session("live"))
  self.now+=61
  with self.assertRaises(PolicyError):self.p.renew("live")
 def test_quickplay_queue(self):
  self.room();self.issue(2);self.p.create("2","DEF456","owner-two","join-two")
  self.assertIsNone(self.p.quickplay("1","ABC123","owner-test",True))
  self.assertEqual(self.p.quickplay("2","DEF456","owner-two",True),"ABC123")
  self.assertIsNone(self.p.quickplay("1","ABC123","owner-test",True))
  self.p.quickplay("1","ABC123","owner-test",False)
  self.assertIsNone(self.p.quickplay("2","DEF456","owner-two",True))
  self.p.quickplay("1","ABC123","owner-test",True)
  self.now+=13
  self.assertIsNone(self.p.quickplay("2","DEF456","owner-two",True))
  with self.assertRaises(PolicyError):self.p.quickplay("2","ABC123","owner-test",True)
  self.p.quickplay("1","ABC123","owner-test",True)
  for n in range(3,6):
   self.issue(n);self.p.reserve(str(n),"ABC123",str(n))
  self.assertIsNone(self.p.quickplay("2","DEF456","owner-two",True))

 def webrtc_room(self):
  self.p.issue("198.51.100.1","host","webrtc");self.p.create("host","RTC123","owner-rtc","join-rtc")
  self.p.issue("198.51.100.2","guest","webrtc");return self.p.reserve("guest","RTC123","ticket")
 def test_webrtc_room_needs_no_udp_endpoint(self):
  reservation=self.webrtc_room()
  self.assertEqual((reservation["address"],reservation["port"]),("RTC123",1))
  self.assertIn("turn:8.8.8.8:3478?transport=udp",self.p.issue("198.51.100.3","other","webrtc")["urls"])
 def test_transports_do_not_mix(self):
  self.webrtc_room();self.issue(5)
  with self.assertRaises(PolicyError):self.p.reserve("5","RTC123","desktop")
  self.room();self.p.issue("198.51.100.6","browser","webrtc")
  with self.assertRaises(PolicyError):self.p.reserve("browser","ABC123","browser")
 def test_webrtc_reservation_outlives_ice(self):
  self.webrtc_room();self.now+=20
  self.assertEqual(self.p.consume("host","RTC123","owner-rtc","ticket"),"guest")
 def test_signal_between_owner_and_reserved_peer(self):
  self.webrtc_room()
  self.p.signal("guest","RTC123","host","offer","v=0 offer")
  self.assertEqual(self.p.drain("host"),[{"from":"guest","room":"RTC123","kind":"offer","sdp":"v=0 offer"}])
  self.assertEqual(self.p.drain("host"),[])
  self.p.signal("host","RTC123","guest","answer","v=0 answer")
  self.assertEqual(self.p.drain("guest")[0]["kind"],"answer")
 def test_signal_rejects_outsiders(self):
  self.webrtc_room();self.p.issue("198.51.100.7","outsider","webrtc")
  with self.assertRaises(PolicyError):self.p.signal("outsider","RTC123","host","offer","x")
  with self.assertRaises(PolicyError):self.p.signal("host","RTC123","outsider","answer","x")
  with self.assertRaises(PolicyError):self.p.signal("guest","RTC123","host","script","x")
  with self.assertRaises(PolicyError):self.p.signal("guest","RTC123","host","offer","x"*12001)
 def test_admitted_peer_keeps_signalling(self):
  self.webrtc_room();self.p.consume("host","RTC123","owner-rtc","ticket")
  self.p.signal("host","RTC123","guest","bye","")
  self.assertEqual(self.p.drain("guest")[0]["kind"],"bye")
 def test_cutoff(self):
  self.room();self.p.cutoff()
  with self.assertRaises(PolicyError):self.p.issue("198.51.100.2","2")
  with self.assertRaises(PolicyError):self.p.heartbeat("1","ABC123","owner-test",1)
if __name__=="__main__":unittest.main()
