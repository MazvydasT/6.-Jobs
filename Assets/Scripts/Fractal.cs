using System.Collections;
using System.Collections.Generic;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;

using UnityEngine;

public class Fractal : MonoBehaviour
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct UpdateFractalLevelJob : IJobFor
    {
        public int levelIndex;

        public float3 position;
        public quaternion rotation;

        public float spinAngleDelta;
        public float scale;

        public NativeArray<FractalPart> levelParts;

        [ReadOnly]
        public NativeArray<FractalPart> parentLevelParts;

        [WriteOnly]
        public NativeArray<float3x4> levelMatrices;

        public void Execute(int partInLevelIndex)
        {
            var part = levelParts[partInLevelIndex];

            part.spinAngle += spinAngleDelta;

            var parent = levelIndex > 0 ? parentLevelParts[partInLevelIndex / 5] : default;

            var parentWorldRotation = levelIndex == 0 ? rotation : parent.worldRotation;

            part.worldRotation = mul(parentWorldRotation, (mul(part.rotation, quaternion.RotateY(part.spinAngle))));

            var r = float3x3(part.worldRotation) * scale;
            levelMatrices[partInLevelIndex] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);

            part.worldPosition = levelIndex == 0 ? position : (parent.worldPosition + mul(parentWorldRotation, 1.5f * scale * part.direction));

            levelParts[partInLevelIndex] = part;
        }
    }

    struct FractalPart
    {
        public float3 direction, worldPosition;
        public quaternion rotation, worldRotation;
        public float spinAngle;
    }

    [SerializeField, Range(1, 8)]
    int depth = 4;

    [SerializeField]
    Mesh mesh = default;

    [SerializeField]
    Material material = default;

    static readonly int matricesId = Shader.PropertyToID("_Matrices");

    static MaterialPropertyBlock propertyBlock;

    static readonly float3[] directions = {
        up(),// Vector3.up,
        right(),// Vector3.right,
        left(),// Vector3.left,
        forward(),// Vector3.forward,
        back()// Vector3.back
    };

    static readonly quaternion[] rotations = {
        quaternion.identity,// Quaternion.identity,
        quaternion.RotateZ(-0.5f * PI),// Quaternion.Euler(0,0,-90),
        quaternion.RotateZ(0.5f * PI),// Quaternion.Euler(0,0,90),
        quaternion.RotateX(0.5f * PI),// Quaternion.Euler(90,0,0),
        quaternion.RotateX(-0.5f * PI)// Quaternion.Euler(-90,0,0)
    };

    NativeArray<FractalPart>[] parts;
    NativeArray<float3x4>[] matrices;
    ComputeBuffer[] matricesBuffers;

    void OnEnable()
    {
        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];
        matricesBuffers = new ComputeBuffer[depth];

        int totalPartCount = 0;

        for (int levelIndex = 0; levelIndex < parts.Length; ++levelIndex)
        {
            var levelPartsCount = levelIndex == 0 ? 1 : parts[levelIndex - 1].Length * 5;

            totalPartCount += levelPartsCount;

            var levelParts = new NativeArray<FractalPart>(levelPartsCount, Allocator.Persistent);
            var levelMatrices = new NativeArray<float3x4>(levelPartsCount, Allocator.Persistent);
            var levelMatricesBuffer = new ComputeBuffer(levelPartsCount, 12 * 4);

            for (int partInLevelIndex = 0, group = 0; partInLevelIndex < levelPartsCount; ++partInLevelIndex, group = partInLevelIndex / 5)
            {
                levelParts[partInLevelIndex] = CreatePart(partInLevelIndex - group * 5);
            }

            parts[levelIndex] = levelParts;
            matrices[levelIndex] = levelMatrices;
            matricesBuffers[levelIndex] = levelMatricesBuffer;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < parts.Length; ++i)
        {
            matricesBuffers[i].Release();
            parts[i].Dispose();
            matrices[i].Dispose();
        }

        parts = null;
        matrices = null;
        matricesBuffers = null;
    }

    void OnValidate()
    {
        if (parts != null && enabled)
        {
            OnDisable();
            OnEnable();
        }
    }

    void Update()
    {
        var spinAngleDelta = 0.125f * PI * Time.deltaTime;

        var scale = transform.localScale.x;

        var bounds = new Bounds(transform.position, 3f * scale * Vector3.one);

        JobHandle jobHandle = default;

        for (int levelIndex = 0, levelCount = parts.Length; levelIndex < levelCount; ++levelIndex)
        {
            var levelParts = parts[levelIndex];
            var parentLevelParts = levelIndex > 0 ? parts[levelIndex - 1] : parts[1];

            var levelMatrices = matrices[levelIndex];

            jobHandle = new UpdateFractalLevelJob
            {
                levelIndex = levelIndex,
                levelMatrices = levelMatrices,
                levelParts = levelParts,
                parentLevelParts = parentLevelParts,
                scale = scale,
                spinAngleDelta = spinAngleDelta,
                position = levelIndex == 0 ? transform.position : default,
                rotation = levelIndex == 0 ? transform.rotation : default
            }.ScheduleParallel(levelParts.Length, 5, jobHandle);

            scale *= 0.5f;
        }

        jobHandle.Complete();

        for (int levelIndex = 0, levelCount = parts.Length; levelIndex < levelCount; ++levelIndex)
        {
            var levelMatrices = matrices[levelIndex];

            var matricesBuffer = matricesBuffers[levelIndex];
            matricesBuffer.SetData(levelMatrices);

            propertyBlock.SetBuffer(matricesId, matricesBuffer);

            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, matricesBuffer.count, propertyBlock);
        }
    }

    FractalPart CreatePart(int childIndex)
    {
        return new FractalPart
        {
            direction = directions[childIndex],
            rotation = rotations[childIndex]
        };
    }
}