using System;

namespace KongZuo.LMLWeek4
{
    [Flags]
    public enum KZW4Evidence
    {
        None = 0,
        Ritual = 1,
        PlacementPhoto = 2,
        WorkRecord = 4
    }

    public enum KZW4AnswerResult
    {
        MissingEvidence,
        PhotoNotAligned,
        InvalidChoice,
        Wrong,
        Correct,
        AlreadySolved
    }

    /// <summary>
    /// Pure C# state machine. It never modifies the team's KZFlags or formal scenes.
    /// The final chapter integration should explicitly map these states to KZGameSession.
    /// </summary>
    public sealed class KZW4PuzzleState
    {
        public const KZW4Evidence RequiredEvidence =
            KZW4Evidence.Ritual | KZW4Evidence.PlacementPhoto | KZW4Evidence.WorkRecord;

        private readonly int correctChoice;
        private KZW4Evidence evidence;
        private bool photoAligned;
        private bool solved;
        private bool echoRunning;
        private bool echoCompleted;

        public KZW4PuzzleState(int answerIndex)
        {
            if (answerIndex < 1 || answerIndex > 3)
                throw new ArgumentOutOfRangeException(nameof(answerIndex), "Choice must be 1..3");
            correctChoice = answerIndex;
        }

        public KZW4Evidence Evidence { get { return evidence; } }
        public bool HasAllEvidence { get { return (evidence & RequiredEvidence) == RequiredEvidence; } }
        public bool HasPhoto { get { return (evidence & KZW4Evidence.PlacementPhoto) != 0; } }
        public bool PhotoAligned { get { return photoAligned; } }
        public bool Solved { get { return solved; } }
        public bool EchoRunning { get { return echoRunning; } }
        public bool EchoCompleted { get { return echoCompleted; } }

        public void Collect(KZW4Evidence clue)
        {
            evidence |= clue & RequiredEvidence;
        }

        public bool MarkPhotoAligned()
        {
            if (!HasPhoto || photoAligned) return false;
            photoAligned = true;
            return true;
        }

        public KZW4AnswerResult Submit(int choice)
        {
            if (!HasAllEvidence) return KZW4AnswerResult.MissingEvidence;
            if (!photoAligned) return KZW4AnswerResult.PhotoNotAligned;
            if (solved) return KZW4AnswerResult.AlreadySolved;
            if (choice < 1 || choice > 3) return KZW4AnswerResult.InvalidChoice;
            if (choice != correctChoice) return KZW4AnswerResult.Wrong;
            solved = true;
            return KZW4AnswerResult.Correct;
        }

        public bool TryBeginEcho()
        {
            if (!solved || echoCompleted || echoRunning) return false;
            echoRunning = true;
            return true;
        }

        public void EndEcho(bool completed)
        {
            // An interrupted echo can be retried, and may not grant completion.
            if (echoRunning && completed) echoCompleted = true;
            echoRunning = false;
        }
    }
}
