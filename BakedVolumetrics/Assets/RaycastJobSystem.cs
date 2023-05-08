using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

public class RaycastJobSystem : MonoBehaviour
{
    [SerializeField] private Transform[] _raycastOrigins;
    [SerializeField] private Vector3[] _raycastDirections;

    private NativeArray<RaycastHit> _raycastHits;
    private JobHandle _jobHandle;

    private void Start()
    {
        _raycastHits = new NativeArray<RaycastHit>(_raycastDirections.Length, Allocator.Persistent);
    }

    private void OnDestroy()
    {
        _raycastHits.Dispose();
    }

    private void Update()
    {
        var raycastJob = new RaycastJobParallel
        {
            Origins = new NativeArray<float3>(_raycastOrigins.Length, Allocator.TempJob),
            Directions = new NativeArray<float3>(_raycastDirections.Length, Allocator.TempJob),
            Hits = _raycastHits
        };

        for (var i = 0; i < _raycastOrigins.Length; i++)
        {
            raycastJob.Origins[i] = _raycastOrigins[i].position;
            raycastJob.Directions[i] = _raycastDirections[i];
        }

        _jobHandle = raycastJob.Schedule(_raycastOrigins.Length, 10);
        JobHandle.ScheduleBatchedJobs();
    }

    private void LateUpdate()
    {
        _jobHandle.Complete();

        for (var i = 0; i < _raycastHits.Length; i++)
        {
            if (_raycastHits[i].collider != null)
            {
                Debug.Log($"Raycast {i} hit {_raycastHits[i].collider.name}");
            }
        }
    }

    private struct RaycastJobParallel : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Origins;
        [ReadOnly] public NativeArray<float3> Directions;
        public NativeArray<RaycastHit> Hits;

        public void Execute(int index)
        {
            var origin = Origins[index];
            var direction = Directions[index];

            if (Physics.Raycast(origin, direction, out var hit))
            {
                Hits[index] = hit;
            }
        }
    }
}