using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace ProjectTowerRpg.ECS.Components
{
    public struct InteractableSpawnMarkerComponent : IComponentData
    {
        public FixedString64Bytes Id;
        public InteractableType Type;
        public float3 SpawnPosition;
        public bool StartOpen;
        public UnityObjectRef<GameObject> PrefabRef; // Ссылка на префаб
    }
}
