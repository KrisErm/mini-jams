using UnityEngine;

namespace MiniJam
{
    [CreateAssetMenu(menuName = "Mini Jam/Game configuration")]
    public sealed class JamConfig : ScriptableObject
    {
        [Header("Visual prefabs (optional)")]
        public GameObject hero;
        public GameObject guard;
        public GameObject princess;
        public GameObject castleExterior;
        public GameObject wallModule;
        public GameObject floorModule;
        public GameObject doorwayModule;
        public bool suppliedArt;
        [Header("Model orientation")]
        public float heroYaw;
        public float guardYaw;
        public float princessYaw;
        [Header("Optional character animation clips")]
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip attack;
        [Header("Gameplay")]
        [Range(3f, 8f)] public float moveSpeed = 5.5f;
        [Range(1f, 3f)] public float jumpHeight = 1.5f;
        [Range(0.5f, 4f)] public float mouseSensitivity = 1f;
        [Range(1f, 5f)] public float guardSpeed = 2.4f;
        [Header("Intro")]
        public string princessToRescue = "Розалинда";
        public string actualPrincess = "Маргарита";
        [Header("Ending (edit these lines in the Inspector)")]
        [TextArea] public string mistakenPrincessLine = "Я не та принцесса, которую ты искал. И меня вообще никто не похищал!";
        [TextArea] public string cleaningBillLine = "С тебя уборка и 18 956 золота. За стражу, решётки и моральный ущерб.";
    }
}
