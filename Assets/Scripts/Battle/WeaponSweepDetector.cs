using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 본에 정의된 선분을 프레임 사이에서 쓸어 빠른 공격도 놓치지 않는 판정기다.
/// 활성 구간 하나당 같은 대상은 한 번만 적중한다.
/// </summary>
public sealed class WeaponSweepDetector : MonoBehaviour
{
    private const int MaxHits = 32;
    private const int MaxPathSamples = 12;

    private readonly RaycastHit[] castHits = new RaycastHit[MaxHits];
    private readonly Collider[] overlapHits = new Collider[MaxHits];
    private readonly HashSet<Transform> hitOwners = new HashSet<Transform>();
    private readonly Vector3[] previousPoints = new Vector3[MaxPathSamples];
    private readonly Vector3[] currentPoints = new Vector3[MaxPathSamples];

    private HitBox hitBox;
    private Transform sweepSource;
    private Vector3 localStart;
    private Vector3 localEnd;
    private float radius;
    private int pathSamples;
    private bool active;

    public bool IsActive => active;
    public Transform SweepSource => sweepSource;
    public float Radius => radius;
    public Vector3 CurrentStart => sweepSource != null
        ? sweepSource.TransformPoint(localStart)
        : transform.position;
    public Vector3 CurrentEnd => sweepSource != null
        ? sweepSource.TransformPoint(localEnd)
        : transform.position;

    public void Initialize(HitBox ownerHitBox)
    {
        hitBox = ownerHitBox;
    }

    public bool Begin(
        Transform source,
        float sweepRadius,
        Vector3 start,
        Vector3 end,
        int samples)
    {
        End();

        if (hitBox == null || source == null)
            return false;

        sweepSource = source;
        radius = Mathf.Max(0.01f, sweepRadius);
        localStart = start;
        localEnd = end;
        pathSamples = Mathf.Clamp(samples, 2, MaxPathSamples);
        CapturePoints(previousPoints);
        hitOwners.Clear();
        active = true;
        hitBox.SetManualActive(true);
        DetectCurrentBlade(previousPoints);
        return true;
    }

    public void Step()
    {
        if (!active || sweepSource == null || hitBox == null)
            return;

        CapturePoints(currentPoints);

        for (int i = 0; i < pathSamples; i++)
            Sweep(previousPoints[i], currentPoints[i]);

        DetectCurrentBlade(currentPoints);
        SwapPointBuffers();
    }

    public void End()
    {
        if (hitBox != null)
            hitBox.SetManualActive(false);

        active = false;
        sweepSource = null;
        hitOwners.Clear();
    }

    private void CapturePoints(Vector3[] destination)
    {
        for (int i = 0; i < pathSamples; i++)
        {
            float t = i / (pathSamples - 1f);
            destination[i] = sweepSource.TransformPoint(Vector3.Lerp(localStart, localEnd, t));
        }
    }

    private void DetectCurrentBlade(Vector3[] points)
    {
        for (int i = 0; i < pathSamples; i++)
            DetectOverlap(points[i]);

        for (int i = 0; i < pathSamples - 1; i++)
            Sweep(points[i], points[i + 1]);
    }

    private void Sweep(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return;

        int count = Physics.SphereCastNonAlloc(
            start,
            radius,
            delta / distance,
            castHits,
            distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
            TryHit(castHits[i].collider, castHits[i].point);
    }

    private void DetectOverlap(Vector3 position)
    {
        int count = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            overlapHits,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
            TryHit(overlapHits[i], overlapHits[i].ClosestPoint(position));
    }

    private void TryHit(Collider candidate, Vector3 impactPoint)
    {
        if (candidate == null)
            return;

        HurtBox hurtBox = candidate.GetComponent<HurtBox>();
        if (hurtBox == null)
            hurtBox = candidate.GetComponentInParent<HurtBox>();
        if (hurtBox == null)
            return;

        Transform owner = hurtBox.OwnerRoot != null
            ? hurtBox.OwnerRoot
            : hurtBox.transform.root;

        if (!hitOwners.Add(owner))
            return;

        if (!hitBox.TryHit(candidate, impactPoint))
            hitOwners.Remove(owner);
    }

    private void SwapPointBuffers()
    {
        for (int i = 0; i < pathSamples; i++)
            previousPoints[i] = currentPoints[i];
    }

    private void OnDisable()
    {
        End();
    }
}