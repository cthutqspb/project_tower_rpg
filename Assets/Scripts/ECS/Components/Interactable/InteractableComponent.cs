using Unity.Entities;
using Unity.Collections;
using UnityEngine; // 🎯 Нужен для GameObject

namespace ProjectTowerRpg.ECS.Components
{
    public struct InteractableComponent : IComponentData
    {
        public FixedString64Bytes Id; 
        public FixedString64Bytes NameKey;
        public InteractableType Type;
        public bool IsInteractable;
        
        // 🎯 Ссылка на префаб для EntityVisibilitySystem
        public UnityObjectRef<GameObject> PrefabRef; 
    }
}
