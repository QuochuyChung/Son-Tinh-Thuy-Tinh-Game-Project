using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    [Serializable]
    public sealed class QuestQuizQuestion
    {
        public string id;
        public string difficulty;
        public string category;
        public string question;
        public string[] options;
        public int correct_index;
        public string explanation;
    }

    public static class QuestQuizDatabase
    {
        [Serializable]
        sealed class Wrapper { public QuestQuizQuestion[] items; }

        static QuestQuizQuestion[] questions;

        public static IReadOnlyList<QuestQuizQuestion> Questions
        {
            get
            {
                if (questions == null) Load();
                return questions;
            }
        }

        static void Load()
        {
            TextAsset source = Resources.Load<TextAsset>("Questions/question");
            if (source == null)
            {
                Debug.LogError("Không tìm thấy Resources/Questions/question.json.");
                questions = Array.Empty<QuestQuizQuestion>();
                return;
            }

            Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"items\":" + source.text + "}");
            questions = wrapper?.items ?? Array.Empty<QuestQuizQuestion>();
        }

        public static List<QuestQuizQuestion> PickThree()
        {
            var pool = new List<QuestQuizQuestion>(Questions);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            if (pool.Count > 3) pool.RemoveRange(3, pool.Count - 3);
            return pool;
        }
    }
}
