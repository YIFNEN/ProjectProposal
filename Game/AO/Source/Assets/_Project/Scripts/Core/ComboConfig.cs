using System;
using UnityEngine;

namespace AO.Core
{
    [CreateAssetMenu(fileName = "ComboConfig", menuName = "AO/Configs/Combo Config", order = 1)]
    public class ComboConfig : ScriptableObject
    {
        [Serializable]
        public struct Tier
        {
            [Tooltip("이 콤보 이상에서 적용")]
            public int MinCombo;
            [Tooltip("점수 배율")]
            public float Multiplier;
        }

        [Header("Multiplier Tiers (정렬 순서대로 적용)")]
        public Tier[] Tiers = new Tier[]
        {
            new Tier { MinCombo = 0,  Multiplier = 1.0f },
            new Tier { MinCombo = 10, Multiplier = 1.5f },
            new Tier { MinCombo = 30, Multiplier = 2.0f },
            new Tier { MinCombo = 50, Multiplier = 3.0f },
        };

        public float GetMultiplier(int combo)
        {
            float result = 1f;
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (combo >= Tiers[i].MinCombo) result = Tiers[i].Multiplier;
                else break;
            }
            return result;
        }
    }
}
