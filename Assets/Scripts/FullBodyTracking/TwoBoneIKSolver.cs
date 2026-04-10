using UnityEngine;

/// <summary>
/// Custom Two-Bone IK solver using the Law of Cosines.
/// Solves a chain: Root → Mid → Tip so that Tip reaches Target,
/// with a Hint controlling the bend direction.
/// </summary>
public class TwoBoneIKSolver
{
    /// <summary>
    /// Solves two-bone IK in-place. Call from LateUpdate after FK has been applied.
    /// </summary>
    /// <param name="root">Root bone transform (e.g. UpperArm / UpperLeg)</param>
    /// <param name="mid">Mid bone transform (e.g. Elbow / Knee)</param>
    /// <param name="tip">Tip bone transform (e.g. Hand / Foot)</param>
    /// <param name="targetPos">World-space position the tip should reach</param>
    /// <param name="targetRot">World-space rotation the tip should have (applied after solve)</param>
    /// <param name="hintPos">World-space position that controls bend direction</param>
    /// <param name="posWeight">Blend weight for position [0-1]</param>
    /// <param name="rotWeight">Blend weight for rotation [0-1]</param>
    /// <param name="hintWeight">Blend weight for hint influence [0-1]</param>
    public static void Solve(
        Transform root, Transform mid, Transform tip,
        Vector3 targetPos, Quaternion targetRot,
        Vector3 hintPos,
        float posWeight = 1f, float rotWeight = 1f, float hintWeight = 1f)
    {
        if (root == null || mid == null || tip == null) return;

        // Cache original positions
        Vector3 aPos = root.position; // root (shoulder / hip)
        Vector3 bPos = mid.position;  // mid  (elbow / knee)
        Vector3 cPos = tip.position;  // tip  (wrist / ankle)

        // Bone lengths (from the bind pose hierarchy)
        float upperLen = (bPos - aPos).magnitude;
        float lowerLen = (cPos - bPos).magnitude;

        // Blend target position with current tip position
        Vector3 blendedTarget = Vector3.Lerp(cPos, targetPos, posWeight);

        // Distance from root to blended target
        float targetDist = (blendedTarget - aPos).magnitude;

        // Clamp target distance to reachable range.
        // Use proportional epsilon for maxReach to guarantee a minimum bend angle
        // (~5-7°) regardless of limb length. This prevents the near-extension
        // singularity where the bend plane becomes undefined and small movements
        // near full extension produce no visible bending.
        float maxReach = upperLen + lowerLen;
        float minReach = Mathf.Abs(upperLen - lowerLen);
        float softMaxReach = maxReach * 0.998f;
        float clampedDist = Mathf.Clamp(targetDist, minReach + 0.001f, softMaxReach);

        // Recalculate blended target if clamping changed the distance
        if (!Mathf.Approximately(clampedDist, targetDist))
        {
            Vector3 dir = (blendedTarget - aPos).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            blendedTarget = aPos + dir * clampedDist;
        }
        targetDist = clampedDist;

        // --- Law of Cosines to find the angle at the mid joint ---
        // Triangle sides: a = lowerLen, b = upperLen, c = targetDist
        // Angle at root (alpha): cos(alpha) = (b² + c² - a²) / (2bc)
        // Angle at mid  (beta):  cos(beta)  = (a² + b² - c²) / (2ab)

        float cosAngleRoot = Mathf.Clamp(
            (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen) /
            (2f * upperLen * targetDist),
            -1f, 1f);

        float cosAngleMid = Mathf.Clamp(
            (upperLen * upperLen + lowerLen * lowerLen - targetDist * targetDist) /
            (2f * upperLen * lowerLen),
            -1f, 1f);

        float angleRoot = Mathf.Acos(cosAngleRoot);
        float angleMid = Mathf.Acos(cosAngleMid);

        // --- Apply rotations ---

        // Direction from root to target
        Vector3 rootToTarget = (blendedTarget - aPos).normalized;
        if (rootToTarget.sqrMagnitude < 0.001f) rootToTarget = Vector3.forward;

        // --- Determine the bend plane from the hint position ---
        // Instead of using Cross(rootToTarget, hintDir) which degenerates when
        // the hint is nearly collinear with the limb axis (common near full
        // extension), we project the hint direction onto the plane perpendicular
        // to rootToTarget. This always yields a stable bend direction as long as
        // the hint isn't exactly on the limb axis.

        Vector3 rootToHint = hintPos - aPos;
        Vector3 hintPerp = Vector3.ProjectOnPlane(rootToHint, rootToTarget);

        if (hintPerp.sqrMagnitude < 0.0001f)
        {
            // Hint lies on the limb axis — fallback to current mid-joint offset
            hintPerp = Vector3.ProjectOnPlane(bPos - aPos, rootToTarget);
        }
        if (hintPerp.sqrMagnitude < 0.0001f)
        {
            hintPerp = Vector3.ProjectOnPlane(Vector3.up, rootToTarget);
        }
        if (hintPerp.sqrMagnitude < 0.0001f)
        {
            hintPerp = Vector3.ProjectOnPlane(Vector3.right, rootToTarget);
        }
        hintPerp = hintPerp.normalized;

        // Bend axis: perpendicular to both rootToTarget and the projected hint.
        // With this axis definition, AngleAxis(+angleRoot, bendAxis) rotates
        // rootToTarget TOWARD hintPerp (mid joint goes toward the hint).
        // The negative sign was a bug: it was rotating AWAY from the hint,
        // causing knees/elbows to bend in the opposite direction from the hint.
        Vector3 bendAxis = Vector3.Cross(rootToTarget, hintPerp).normalized;

        // Rotate rootToTarget by +angleRoot around bendAxis so the mid joint
        // moves in the hint direction.
        Vector3 upperDir = Quaternion.AngleAxis(angleRoot * Mathf.Rad2Deg, bendAxis) * rootToTarget;

        // New mid position
        Vector3 newBPos = aPos + upperDir * upperLen;

        // Blend hint influence
        if (hintWeight < 1f)
        {
            newBPos = Vector3.Lerp(bPos, newBPos, hintWeight);
            // Recalculate to maintain bone length
            upperDir = (newBPos - aPos).normalized;
            newBPos = aPos + upperDir * upperLen;
        }

        // --- Apply root rotation ---
        // Rotate root so that its child (mid) moves from bPos to newBPos
        Quaternion rootRotDelta = Quaternion.FromToRotation(bPos - aPos, newBPos - aPos);
        root.rotation = rootRotDelta * root.rotation;

        // Update mid position after root rotation
        // (mid.position should now be close to newBPos due to hierarchy)

        // --- Apply mid rotation ---
        // After root rotation, tip position has moved. We need mid to rotate
        // so that tip goes from its current position to blendedTarget.
        Vector3 currentTipPos = tip.position;
        Vector3 midToCurrentTip = (currentTipPos - mid.position).normalized;
        Vector3 midToTarget = (blendedTarget - mid.position).normalized;

        if (midToCurrentTip.sqrMagnitude > 0.001f && midToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion midRotDelta = Quaternion.FromToRotation(midToCurrentTip, midToTarget);
            mid.rotation = midRotDelta * mid.rotation;
        }

        // --- Apply tip rotation ---
        if (rotWeight > 0f)
        {
            tip.rotation = Quaternion.Slerp(tip.rotation, targetRot, rotWeight);
        }
    }

    /// <summary>
    /// Calculates a default hint position for a limb given the bend preference direction.
    /// </summary>
    /// <param name="root">Root bone position</param>
    /// <param name="mid">Mid bone position</param>
    /// <param name="tip">Tip bone position</param>
    /// <param name="preferredBendDir">World-space direction the joint should bend towards</param>
    /// <param name="hintDistance">How far from the mid joint the hint should be</param>
    /// <returns>World-space hint position</returns>
    public static Vector3 CalculateHintPosition(
        Vector3 root, Vector3 mid, Vector3 tip,
        Vector3 preferredBendDir, float hintDistance = 0.5f)
    {
        // Project preferred direction onto the plane perpendicular to root→tip
        Vector3 limbDir = (tip - root).normalized;
        Vector3 projected = Vector3.ProjectOnPlane(preferredBendDir, limbDir).normalized;

        if (projected.sqrMagnitude < 0.001f)
        {
            // Fallback: use existing mid offset from the root-tip line
            Vector3 midOnLine = root + Vector3.Project(mid - root, limbDir);
            projected = (mid - midOnLine).normalized;
            if (projected.sqrMagnitude < 0.001f)
                projected = Vector3.forward;
        }

        return mid + projected * hintDistance;
    }
}
