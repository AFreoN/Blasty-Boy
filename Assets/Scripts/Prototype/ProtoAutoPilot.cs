#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

// Editor-only bot that plays the prototype: picks the best bend for each throw, and optionally screenshots key
// moments at exact game times. Started from the Unity CLI, e.g.
//   ProtoAutoPilot.Run("Temp/caps", 2)   -> plays a full run, capturing the first 2 throws of every wave.
// Results are written to the console and the playtest log, so it doubles as a quick balance check.
public class ProtoAutoPilot : MonoBehaviour
{
    public static string lastReport = "";

    string folder;
    int capturesPerWave;
    float aimHold = 0.35f;

    public static ProtoAutoPilot Run(string captureFolder, int capturesPerWave)
    {
        var go = new GameObject("AutoPilot");
        var pilot = go.AddComponent<ProtoAutoPilot>();
        pilot.folder = captureFolder;
        pilot.capturesPerWave = capturesPerWave;
        lastReport = "running";
        return pilot;
    }

    IEnumerator Start()
    {
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        ProtoGame game = ProtoGame.instance;
        ProtoThrower thrower = ProtoThrower.instance;
        if (game.state == ProtoState.Menu)
            game.StartRun();

        int lastWave = -1, throwInWave = 0, throws = 0, chasesCaptured = 0;
        Capture("intro");

        while (game.state != ProtoState.Won && game.state != ProtoState.Lost)
        {
            if (game.state == ProtoState.Chase && chasesCaptured <= game.WaveIndex && capturesPerWave > 0)
            {
                chasesCaptured = game.WaveIndex + 1;
                foreach (float at in new[] { 0.8f, 1.6f, 2.3f })
                {
                    yield return new WaitForSecondsRealtime(at - (at > 0.8f ? (at > 1.6f ? 1.6f : 0.8f) : 0f));
                    Capture("chase" + (game.WaveIndex + 1) + "_" + Mathf.RoundToInt(at * 1000) + "ms");
                }
                continue;
            }

            if (!ProtoGame.IsPlaying || thrower.Quiver < 1f)
            {
                yield return null;
                continue;
            }

            if (game.WaveIndex != lastWave)
            {
                lastWave = game.WaveIndex;
                throwInWave = 0;
            }

            float bend = thrower.FindBestBend(out int predicted, out bool useful);
            if (!useful)
            {
                // Nothing reachable yet (enemies still dropping in): stop aiming and wait.
                thrower.SimulateRelease();
                yield return new WaitForSecondsRealtime(0.25f);
                continue;
            }

            bool capture = throwInWave < capturesPerWave;
            string tag = "w" + (game.WaveIndex + 1) + "_t" + (throwInWave + 1);
            yield return new WaitForSecondsRealtime(aimHold);
            thrower.FindBestBend(out predicted);
            if (capture)
            {
                Capture(tag + "_aim_x" + predicted);
                yield return new WaitForEndOfFrame();   // screenshot is taken at end of frame; release after it
                yield return null;
            }

            thrower.SimulateRelease();
            throws++;
            throwInWave++;
            Debug.Log("[AutoPilot] " + tag + " bend=" + bend.ToString("0.00") + " predicted=" + predicted);

            if (capture)
            {
                float t0 = Time.unscaledTime;
                foreach (float at in new[] { 0.3f, 0.55f, 0.8f, 1.1f })
                {
                    while (Time.unscaledTime - t0 < at)
                        yield return null;
                    Capture(tag + "_" + Mathf.RoundToInt(at * 1000) + "ms");
                }
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        yield return new WaitForSecondsRealtime(2f);
        Capture("end");
        lastReport = game.state + " after " + throws + " throws, score " + game.Score + ", hearts " + game.Hearts
            + ", kills predicted/actual " + game.PredictedTotal + "/" + game.ActualTotal;
        Debug.Log("[AutoPilot] " + lastReport);
        Destroy(gameObject);
    }

    void Capture(string name)
    {
        if (!string.IsNullOrEmpty(folder))
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
    }
}
#endif
