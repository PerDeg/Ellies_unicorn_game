using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnicornGame {

// ══════════════════════════════════════
//  SCORE API — talks to the same Express backend as the JS game.
//  Set ServerBase to e.g. "http://192.168.1.50:3000" to enable;
//  leave empty to play fully offline (localStorage-style PlayerPrefs
//  toplist still works).
// ══════════════════════════════════════
public static class ScoreApi {

    // ← point this at your Unraid server to share the global toplist
    public const string ServerBase = "";

    [System.Serializable]
    public class ScorePayload {
        public string name;
        public int score;
        public string difficulty;
        public int maxStreak;
        public int maxMulti;
        public int perfectRounds;
        public int caught;
    }

    public static bool Enabled => !string.IsNullOrEmpty(ServerBase);

    public static IEnumerator Submit(ScorePayload p) {
        if (!Enabled) yield break;
        string json = JsonUtility.ToJson(p);
        using (var req = new UnityWebRequest(ServerBase + "/api/scores", "POST")) {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 8;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("Score submit failed: " + req.error);
        }
    }
}
}
