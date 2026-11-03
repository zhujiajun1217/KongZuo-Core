using UnityEngine;

namespace KongZuo.LMLWeek4
{
    /// <summary>
    /// Match the 1992 photo's landmark positions in normalized viewport space.
    /// The distance check avoids matching from a completely different room.
    /// </summary>
    public static class KZW4PhotoAlignment
    {
        public static Vector3 ProjectReference(Camera view, Transform photoVantage, Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(photoVantage.rotation) * (point - photoVantage.position);
            if (local.z <= 0.001f) return new Vector3(-1f, -1f, -1f);
            float tanHalf = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
            float halfY = local.z * tanHalf;
            float halfX = halfY * view.aspect;
            return new Vector3(.5f + local.x / (2f * halfX),
                               .5f + local.y / (2f * halfY), local.z);
        }

        public static bool Matches(Camera view, Transform photoVantage, Transform[] landmarks,
            float maxDistance, float maxViewportError, out float worstError)
        {
            worstError = float.PositiveInfinity;
            if (view == null || photoVantage == null || landmarks == null || landmarks.Length < 3)
                return false;
            if (Vector3.Distance(view.transform.position, photoVantage.position) > maxDistance)
                return false;

            float maxError = 0f;
            for (int i = 0; i < landmarks.Length; i++)
            {
                if (landmarks[i] == null) return false;
                Vector3 target = ProjectReference(view, photoVantage, landmarks[i].position);
                Vector3 actual = view.WorldToViewportPoint(landmarks[i].position);
                if (target.z <= 0f || actual.z <= 0f ||
                    target.x < 0f || target.x > 1f || target.y < 0f || target.y > 1f ||
                    actual.x < 0f || actual.x > 1f || actual.y < 0f || actual.y > 1f)
                    return false;
                float e = Vector2.Distance(new Vector2(target.x, target.y),
                                           new Vector2(actual.x, actual.y));
                maxError = Mathf.Max(maxError, e);
            }
            worstError = maxError;
            return maxError <= Mathf.Max(0f, maxViewportError);
        }
    }
}
