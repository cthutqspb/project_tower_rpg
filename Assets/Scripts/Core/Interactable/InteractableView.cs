using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Presentation;

namespace ProjectTowerRpg.ECS.Systems
{
    public class InteractableView : MonoBehaviour
    {
        [HideInInspector][System.NonSerialized] public Entity Entity;
        public string AnimatorBoolName = "IsOpen";

        private EntityManager _em;
        private Animator _animator;
        private bool _lastState = false;
        private bool _isLinked = false;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        // 🎯 ВЫЗЫВАЕТСЯ ИЗ EntityVisibilitySystem
        public void LinkToEntity(Entity boundEntity)
        {
            if (boundEntity == Entity.Null) return;
            
            Entity = boundEntity;
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityViewRegistry.Register(Entity, this);
            _isLinked = true;

            SyncState(true); 
        }

        private void SyncState(bool instant)
        {
            if (Entity == Entity.Null || !_em.Exists(Entity)) return;
            bool currentState = _em.HasComponent<IsOpenTag>(Entity);
            _lastState = currentState;

            if (_animator != null)
            {
                if (instant)
                    _animator.Play(currentState ? "gate_open" : "gate_closed", 0, 1f); // Используем твои имена стейтов!
                else
                    _animator.SetBool(AnimatorBoolName, currentState);
            }
        }

        private void Update()
        {
            if (!_isLinked || Entity == Entity.Null || !_em.Exists(Entity)) return;

            bool currentState = _em.HasComponent<IsOpenTag>(Entity);

            if (currentState != _lastState)
            {
                _lastState = currentState;
                if (_animator != null)
                {
                    _animator.SetBool(AnimatorBoolName, currentState);
                }
            }
        }

        private void OnDestroy()
        {
            if (_isLinked && Entity != Entity.Null)
                EntityViewRegistry.Unregister(Entity);
        }
    }
}
