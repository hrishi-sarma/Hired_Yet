var VoiceWeb = {
  $VW: {
    rec: null, want: false, go: 'VoiceInput', warm: false,
    send: function (m, s) {
      if (typeof Module !== 'undefined' && Module.SendMessage) Module.SendMessage(VW.go, m, s);
      else if (typeof SendMessage === 'function') SendMessage(VW.go, m, s);
    }
  },

  VoiceWeb_IsSupported: function () {
    return (window.SpeechRecognition || window.webkitSpeechRecognition) ? 1 : 0;
  },

  VoiceWeb_Arm: function () {
    if (VW.warm) return; VW.warm = true;
    var c = document.querySelector('canvas');
    var h = function () {
      if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        navigator.mediaDevices.getUserMedia({ audio: true })
          .then(function (s) { s.getTracks().forEach(function (t) { t.stop(); }); })
          .catch(function () {});
      }
    };
    if (c) c.addEventListener('pointerup', h, { once: true, capture: true });
  },

  VoiceWeb_Start: function (goPtr, langPtr) {
    VW.go = UTF8ToString(goPtr);
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { VW.send('OnVoiceError', 'unsupported'); return; }
    VW.want = true;
    var r = new SR();
    VW.rec = r;
    r.lang = UTF8ToString(langPtr);
    r.continuous = true;
    r.interimResults = true;
    r.maxAlternatives = 1;

    r.onresult = function (e) {
      var f = '', i = '', conf = 0, n = 0;
      for (var k = e.resultIndex; k < e.results.length; k++) {
        var res = e.results[k];
        if (res.isFinal) { f += res[0].transcript + ' '; conf += res[0].confidence; n++; }
        else i += res[0].transcript;
      }
      VW.send('OnVoiceResult', JSON.stringify({ f: f.trim(), i: i.trim(), c: n ? conf / n : -1 }));
    };
    r.onerror = function (e) {
      if (e.error === 'no-speech' || e.error === 'aborted') return;
      VW.want = false;
      VW.send('OnVoiceError', e.error);
    };
    r.onend = function () {
      if (VW.want) { try { r.start(); } catch (x) {} }
      else VW.send('OnVoiceEnded', '');
    };
    try { r.start(); } catch (x) { VW.send('OnVoiceError', 'start-failed'); }
  },

  VoiceWeb_Stop: function () {
    VW.want = false;
    if (VW.rec) { try { VW.rec.stop(); } catch (x) {} }
  }
};
autoAddDeps(VoiceWeb, '$VW');
mergeInto(LibraryManager.library, VoiceWeb);
