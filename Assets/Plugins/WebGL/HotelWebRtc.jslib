// Browser side of HotelWebRtcSocketFactory: one RTCPeerConnection per remote player, each carrying
// Mirage packets over an unordered, unreliable data channel. Mirage supplies reliability itself.
// Signalling is non-trickle: a description is handed to C# only after ICE gathering finishes.
var HotelWebRtc = {
    $HFRtc: {
        peers: {},
        next: 1,
        gatherTimeoutMs: 5000,
        maxQueued: 256,

        config: function (iceJson, policy) {
            var servers = [];
            try { servers = JSON.parse(iceJson); } catch (e) { }
            return { iceServers: servers, iceTransportPolicy: policy === "relay" ? "relay" : "all" };
        },

        register: function (pc) {
            var id = HFRtc.next++;
            var peer = { pc: pc, channel: null, inbox: [], local: null, failed: false };
            HFRtc.peers[id] = peer;
            pc.onconnectionstatechange = function () {
                if (pc.connectionState === "failed" || pc.connectionState === "closed") peer.failed = true;
            };
            return id;
        },

        attach: function (peer, channel) {
            channel.binaryType = "arraybuffer";
            channel.onmessage = function (event) {
                if (peer.inbox.length < HFRtc.maxQueued) peer.inbox.push(new Uint8Array(event.data));
            };
            channel.onclose = function () { peer.failed = true; };
            peer.channel = channel;
        },

        // Resolve once gathering completes (or times out) so the description carries relay candidates.
        publish: function (peer) {
            var pc = peer.pc;
            var done = false;
            function finish() {
                if (done) return;
                done = true;
                peer.local = pc.localDescription ? pc.localDescription.sdp : null;
            }
            if (pc.iceGatheringState === "complete") { finish(); return; }
            pc.addEventListener("icegatheringstatechange", function () {
                if (pc.iceGatheringState === "complete") finish();
            });
            setTimeout(finish, HFRtc.gatherTimeoutMs);
        },

        fail: function (peer, error) {
            console.warn("Hotel WebRTC: " + error);
            peer.failed = true;
        },

        returnString: function (value) {
            var size = lengthBytesUTF8(value) + 1;
            var buffer = _malloc(size);
            stringToUTF8(value, buffer, size);
            return buffer;
        }
    },

    HFRtc_Supported: function () {
        return typeof RTCPeerConnection === "function" ? 1 : 0;
    },

    // Joining player: create the channel and an offer for the host.
    HFRtc_Offer: function (iceJson, policy) {
        var pc;
        try { pc = new RTCPeerConnection(HFRtc.config(UTF8ToString(iceJson), UTF8ToString(policy))); }
        catch (e) { console.warn("Hotel WebRTC: " + e); return 0; }
        var id = HFRtc.register(pc);
        var peer = HFRtc.peers[id];
        HFRtc.attach(peer, pc.createDataChannel("mirage", { ordered: false, maxRetransmits: 0 }));
        pc.createOffer()
            .then(function (offer) { return pc.setLocalDescription(offer); })
            .then(function () { HFRtc.publish(peer); })
            .catch(function (e) { HFRtc.fail(peer, e); });
        return id;
    },

    // Host: answer a joining player's offer.
    HFRtc_Answer: function (iceJson, policy, offerSdp) {
        var pc;
        try { pc = new RTCPeerConnection(HFRtc.config(UTF8ToString(iceJson), UTF8ToString(policy))); }
        catch (e) { console.warn("Hotel WebRTC: " + e); return 0; }
        var id = HFRtc.register(pc);
        var peer = HFRtc.peers[id];
        pc.ondatachannel = function (event) { HFRtc.attach(peer, event.channel); };
        pc.setRemoteDescription({ type: "offer", sdp: UTF8ToString(offerSdp) })
            .then(function () { return pc.createAnswer(); })
            .then(function (answer) { return pc.setLocalDescription(answer); })
            .then(function () { HFRtc.publish(peer); })
            .catch(function (e) { HFRtc.fail(peer, e); });
        return id;
    },

    HFRtc_Accept: function (id, answerSdp) {
        var peer = HFRtc.peers[id];
        if (!peer) return;
        peer.pc.setRemoteDescription({ type: "answer", sdp: UTF8ToString(answerSdp) })
            .catch(function (e) { HFRtc.fail(peer, e); });
    },

    // Returns the gathered local description once, or null while gathering.
    HFRtc_TakeLocal: function (id) {
        var peer = HFRtc.peers[id];
        if (!peer || peer.local === null) return null;
        var sdp = peer.local;
        peer.local = null;
        return HFRtc.returnString(sdp);
    },

    // 0 connecting, 1 open, 2 failed or closed.
    HFRtc_State: function (id) {
        var peer = HFRtc.peers[id];
        if (!peer || peer.failed) return 2;
        return peer.channel && peer.channel.readyState === "open" ? 1 : 0;
    },

    HFRtc_Send: function (id, pointer, length) {
        var peer = HFRtc.peers[id];
        if (!peer || !peer.channel || peer.channel.readyState !== "open") return;
        try { peer.channel.send(HEAPU8.slice(pointer, pointer + length)); }
        catch (e) { HFRtc.fail(peer, e); }
    },

    // Copies the next packet into the buffer; returns its length, 0 when empty, -1 if it did not fit.
    HFRtc_Receive: function (id, pointer, capacity) {
        var peer = HFRtc.peers[id];
        if (!peer || peer.inbox.length === 0) return 0;
        var packet = peer.inbox.shift();
        if (packet.length > capacity) return -1;
        HEAPU8.set(packet, pointer);
        return packet.length;
    },

    HFRtc_Close: function (id) {
        var peer = HFRtc.peers[id];
        if (!peer) return;
        delete HFRtc.peers[id];
        try { if (peer.channel) peer.channel.close(); peer.pc.close(); } catch (e) { }
    }
};

autoAddDeps(HotelWebRtc, "$HFRtc");
mergeInto(LibraryManager.library, HotelWebRtc);
