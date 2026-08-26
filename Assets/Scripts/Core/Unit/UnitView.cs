using UnityEngine;
using Unity.Entities;

namespace ProjectTowerRpg.ECS.Systems
{
    public class UnitView : MonoBehaviour
    {
        [Header("Unit View Passport")]
        public string uid;      // Забивается для игрока ("player") или генерируется чанком
        public string unitId;   // Забивается в IDE Unity ("skeleton_warrior", "skeleton_mage")

        [HideInInspector] 
        public bool IsLinked = false; // Флаг-замок: чтобы система не пыталась привязать сущность дважды
        [System.NonSerialized] public Entity entity;
    }
}

