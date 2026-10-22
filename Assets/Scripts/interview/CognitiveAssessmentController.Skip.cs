// Used by the DevConsole "skip" command: finishes the cognitive puzzles with a perfect score.
public partial class CognitiveAssessmentController
{
    public void ForcePass()
    {
        if (!isRunning) return;
        StopAllCoroutines();
        ClearViewport();
        runResult.gamesTotal = 3;
        runResult.gamesPassed = 3;
        runResult.levelsCleared = 9;
        runResult.levelsFailed = 0;
        runResult.netSuspicionDelta = 0f;
        for (int i = 0; i < runResult.gamePassed.Length; i++) runResult.gamePassed[i] = true;
        overlayRoot.SetActive(false);
        isRunning = false;
        ResumeWorldControls();
        if (onCompletedCallback != null) onCompletedCallback(runResult);
    }
}
