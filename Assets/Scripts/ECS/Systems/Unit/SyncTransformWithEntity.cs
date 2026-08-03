using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics; // Обязательно для математики векторов

public class SyncTransformWithEntity : MonoBehaviour
{
    private EntityManager _entityManager;
    private EntityQuery _playerQuery;
    private Entity _playerEntity;
    private Animator _animator; // Ссылка на наш визуальный аниматор
    private bool _isInitialized = false;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // Находим аниматор, который висит на этом же GameObject (Amy)
        _animator = GetComponent<Animator>();

        _playerQuery = _entityManager.CreateEntityQuery(new ComponentType[] 
        { 
            ComponentType.ReadOnly<LocalTransform>() 
        });
    }

    void LateUpdate()
    {
        if (!_isInitialized)
        {
            if (!_playerQuery.IsEmpty)
            {
                var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                _playerEntity = entities[0]; // <-- ФИКС ЗДЕСЬ
                entities.Dispose();
                _isInitialized = true;
            }
            return;
        }

        if (_entityManager.Exists(_playerEntity))
        {
            var localTransform = _entityManager.GetComponentData<LocalTransform>(_playerEntity);
            
            // Синхронизируем положение в пространстве (твой рабочий код)
            transform.position = localTransform.Position;
            transform.rotation = localTransform.Rotation;

            // 🧬 СИНХРОНИЗАЦИЯ 2D-АНИМАЦИИ С ECS:
            if (_entityManager.HasComponent<ProjectTowerRpg.ECS.Components.MovementComponent>(_playerEntity))
            {
                var moveData = _entityManager.GetComponentData<ProjectTowerRpg.ECS.Components.MovementComponent>(_playerEntity);
                
                // В Unity 6.6 и чистом C# мы берем вектор направления ввода (W,A,S,D) 
                // и скармливаем его компоненты напрямую в параметры нашего Blend Tree!
                if (_animator != null)
                {
                    // moveData.direction.x — это горизонтальная ось (A/D, влево/вправо)
                    _animator.SetFloat("VelocityX", moveData.direction.x);
                    
                    // moveData.direction.z — это вертикальная ось (W/S, вперед/назад)
                    _animator.SetFloat("VelocityZ", moveData.direction.z);

                    _animator.SetBool("IsGrounded", moveData.isGrounded);
                    _animator.SetFloat("VelocityY", moveData.direction.y);
                }
            }
        }
     
    }
}


