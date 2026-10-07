import concurrent.futures, json, threading, unittest, urllib.request, urllib.error
from http.server import ThreadingHTTPServer
from lobby_server import Directory, Handler, RoomError

class DirectoryTests(unittest.TestCase):
    def setUp(self):
        self.now=10
        self.directory=Directory(clock=lambda:self.now)
        self.room=self.directory.execute("POST","/rooms",dict(address="127.0.0.1",port=7777,capacity=2))
    def call(self,method,suffix="",**data):
        return self.directory.execute(method,"/rooms/"+self.room["code"]+suffix,data)
    def test_resolve_code(self):
        self.assertRegex(self.room["code"],r"^[A-Z0-9]{6}$")
        resolved=self.directory.execute("GET","/rooms/"+self.room["code"].lower(),{})
        self.assertEqual(resolved["joinKey"],self.room["joinKey"])
        self.assertNotIn("ownerKey",resolved)
    def test_bad_or_missing_code(self):
        for code,status in [("abc",400),("!!!!!!",400),("ZZZZZZ",404)]:
            if code==self.room["code"]: continue
            with self.assertRaises(RoomError) as failure:
                self.directory.execute("GET","/rooms/"+code,{})
            self.assertEqual(failure.exception.status,status)
    def test_full_and_reopen(self):
        self.call("POST","/heartbeat",ownerKey=self.room["ownerKey"],players=2)
        with self.assertRaises(RoomError) as failure: self.call("GET")
        self.assertEqual(failure.exception.status,409)
        self.call("POST","/heartbeat",ownerKey=self.room["ownerKey"],players=1)
        self.assertEqual(self.call("GET")["players"],1)
    def test_expiry(self):
        self.now+=29
        self.call("POST","/heartbeat",ownerKey=self.room["ownerKey"],players=1)
        self.now+=29
        self.call("GET")
        self.now+=2
        with self.assertRaises(RoomError): self.call("GET")
    def test_owner_only_mutations(self):
        for method,suffix in [("DELETE",""),("POST","/heartbeat")]:
            with self.assertRaises(RoomError) as failure: self.call(method,suffix,ownerKey="wrong",players=1)
            self.assertEqual(failure.exception.status,403)
        self.call("DELETE",ownerKey=self.room["ownerKey"])
        with self.assertRaises(RoomError): self.call("GET")
    def test_invalid_capacity_and_count(self):
        with self.assertRaises(RoomError):
            self.directory.execute("POST","/rooms",dict(address="localhost",port=7777,capacity=1))
        with self.assertRaises(RoomError):
            self.call("POST","/heartbeat",ownerKey=self.room["ownerKey"],players=99)
    def test_quickplay_queue(self):
        self.directory.rooms[self.room["code"]]["capacity"]=4
        def beat(room, enabled=True, count=1):
            return self.directory.execute("POST","/rooms/"+room["code"]+"/heartbeat",
                dict(ownerKey=room["ownerKey"],players=count,quickplay=enabled))
        self.assertNotIn("status",beat(self.room))
        second=self.directory.execute("POST","/rooms",dict(address="localhost",port=7778,capacity=4))
        self.assertEqual(beat(second)["code"],self.room["code"])
        self.assertEqual(beat(second)["status"],"matched")
        beat(self.room,False)
        self.assertNotIn("status",beat(second))
        beat(self.room)
        self.now+=13
        self.assertNotIn("status",beat(second))
        beat(self.room,True,4)
        self.assertNotIn("status",beat(second))

    def test_concurrent_creation(self):
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            rooms=list(pool.map(lambda _:self.directory.execute("POST","/rooms",dict(address="localhost",port=7777,capacity=4)),range(100)))
        self.assertEqual(len({r["code"] for r in rooms}),100)

class HttpTests(unittest.TestCase):
    def test_real_http_create_join_close(self):
        Handler.directory=Directory()
        server=ThreadingHTTPServer(("127.0.0.1",0),Handler)
        worker=threading.Thread(target=server.serve_forever,daemon=True)
        worker.start()
        url=f"http://127.0.0.1:{server.server_port}"
        def request(path,method="GET",body=None):
            req=urllib.request.Request(url+path,method=method,data=json.dumps(body).encode() if body else None,
                                       headers={"Content-Type":"application/json"})
            with urllib.request.urlopen(req) as response: return json.load(response)
        try:
            room=request("/rooms","POST",dict(address="127.0.0.1",port=7777,capacity=4))
            self.assertEqual(request("/rooms/"+room["code"])["joinKey"],room["joinKey"])
            request("/rooms/"+room["code"],"DELETE",dict(ownerKey=room["ownerKey"]))
            with self.assertRaises(urllib.error.HTTPError) as failure: request("/rooms/"+room["code"])
            self.assertEqual(failure.exception.code,404)
        finally:
            server.shutdown()
            server.server_close()
            worker.join()
if __name__=="__main__": unittest.main()

