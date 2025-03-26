using System;
using UnityEngine;

public class FiringSolution
{
    public Nullable<Vector3> Calculate(Vector3 start, Vector3 end, float muzzleV, Vector3 gravity)
    {
        Vector3 delta = end - start;

        // Coefficients of quadratic equation
        float a = gravity.sqrMagnitude;
        float b = -4 * (Vector3.Dot(gravity, delta) + muzzleV * muzzleV);
        float c = 4 * delta.sqrMagnitude;

        float discriminant = b * b - 4 * a * c;
        if (discriminant < 0) // No real solutions
        {
            return null;
        }

        float time0 = Mathf.Sqrt((-b + Mathf.Sqrt(discriminant)) / (2 * a));
        float time1 = Mathf.Sqrt((-b - Mathf.Sqrt(discriminant)) / (2 * a));

        // If no positive solutions, return null; else use minimum positive solution
        float timeToTarget;
        if (time0 < 0 && time1 < 0)
        {
            return null;
        }
        else if (time0 < 0)
        {
            timeToTarget = time1;
        }
        else if (time1 < 0)
        {
            timeToTarget = time0;
        }
        else
        {
            timeToTarget = Mathf.Max(time0, time1);
        }

        return (2 * delta - gravity * timeToTarget * timeToTarget) / (2 * muzzleV * timeToTarget);
    }
}
