using UnityEngine;
using Unity.Entities;

namespace ProjectTowerRpg.ECS.Systems
{
    public class ItemView : MonoBehaviour
    {
        [Header("Item View Passport")]
        public string uid;      // Уникальный строковый ID ("i_123_456")
        public string itemId;   // Идентификатор из JSON-базы ("iron_sword")

        [HideInInspector] 
        public bool IsLinked = false;
        
        [HideInInspector]
        [System.NonSerialized] public Entity Entity = Entity.Null;

        // Автоматически вычисляем хэш при изменении uid в редакторе
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(uid))
            {
                // Просто для отладки, чтобы видеть хэш в инспекторе
                // Сам хэш вычисляется на лету в LinkSystem
            }
        }
        #endif
    }
}
