using System;

[Serializable]
public class ProofInfo
{
    public string item;
    public string testTask;
    public string helpTask;
}

[Serializable]
public class QuestionData
{
    public string id;
    public string bucket;
    public string difficulty;
    public string type;
    public string text;
    public string correct;
    public string[] accepted;
    public string[] tags;
}

[Serializable]
public class JobData
{
    public string id;
    public string[] displayVariants;
    public string tone;
    public string category;
    public int strength;
    public string[] claimTags;
    public ProofInfo proof;
    public QuestionData[] questions;
}

[Serializable]
public class SuspicionConfig
{
    public int correctEasy;
    public int correctHard;
    public int wrongYesNoEasy;
    public int wrongYesNoHard;
    public int wrongShortEasy;
    public int wrongShortHard;
    public int wrongSatiricalEasy;
    public int wrongSatiricalHard;
    public int hedge;
    public int noAnswer;
    public int maxSuspicion;
}

[Serializable]
public class ParsingConfig
{
    public string[] yesWords;
    public string[] noWords;
    public string[] hedgeWords;
}

[Serializable]
public class DrawSlot
{
    public string bucket;
    public string difficulty;
    public string type;
}

[Serializable]
public class DrawConfig
{
    public int questionsPerRun;
    public int tagCooldown;
    public int minYes;
    public int minNo;
    public DrawSlot[] slots;
}

[Serializable]
public class InterviewConfig
{
    public float[] strengthMultipliers;
    public SuspicionConfig suspicion;
    public ParsingConfig parsing;
    public DrawConfig draw;
}

[Serializable]
public class InterviewDatabase
{
    public int version;
    public InterviewConfig config;
    public JobData[] jobs;
}
