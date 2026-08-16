using UnityEngine;
using Unity.Entities;

namespace ProjectTowerRpg.ECS.Systems
{
    public class ItemView : MonoBehaviour
    {
        [Header("Item View Passport")]
        public string uid;      // Генерируется чанком при дропе или пишется руками на сцене
        public string itemId;   // Идентификатор из JSON-базы ("iron_sword", "crystal_sword")

        [HideInInspector] 
        public bool IsLinked = false; // Замок: чтобы линковщик не привязал дважды
        
        [HideInInspector]
        public Entity Entity = Entity.Null; // 🌟 Наш живой ECS-паспорт с большой буквы!
    }
}

