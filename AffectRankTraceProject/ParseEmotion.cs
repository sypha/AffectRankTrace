using System.Collections.Generic;

namespace AffectRankTraceProject
{
    class ParseEmotion
    {
        private List<Emotion> emotionList = new List<Emotion>();

        public ParseEmotion(string path)
        {
            string[] csvLines = System.IO.File.ReadAllLines(path);

            for (int i = 1; i < csvLines.Length; i++)
            {
                Emotion emo = new Emotion(csvLines[i]);

                this.emotionList.Add(emo);
            }
        }
        /// <summary>
        /// Return the emotion tags with dimension values within the defined range
        /// </summary>
        public List<string> GetEmotionTags(double pleasure, double arousal, double dominance)
        {
            var emotionTags = new List<string>();

            foreach (Emotion emo in emotionList)
            {
                if ((emo.value[0] >= GetMin(pleasure) && emo.value[0] <= GetMax(pleasure)) &&
                    (emo.value[1] >= GetMin(arousal) && emo.value[1] <= GetMax(arousal)) &&
                    (emo.value[2] >= GetMin(dominance) && emo.value[2] <= GetMax(dominance)))
                {
                    emotionTags.Add(emo.tag);
                }
            }

            return emotionTags;
        }

        /// <summary>
        /// Converts the emotion dimension value selected by the user to define the range for the emotion tags
        /// </summary>
        private double GetMin(double dimValue)
        {
            double adjustment = dimValue <= 0 ? -0.3 : 0.3;
            double multiplier = dimValue <= 0 ? dimValue : dimValue - 1;
            return ((multiplier * 0.9) + adjustment) / 3;
        }

        private double GetMax(double dimValue)
        {
            double adjustment = dimValue < 0 ? -0.3 : 0.3;
            double offset = dimValue < 0 ? 1 : 0;
            return (((dimValue + offset) * 0.9) + adjustment) / 3;
        }
    }
}
