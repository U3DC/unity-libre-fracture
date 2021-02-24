using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct RotationBounds
{
    public Bounds baseBounds;

    public Vector3 rotation;

    public Vector3 RotatedExtents
    {
        get
        {
            return Quaternion.Euler(rotation) * (baseBounds.extents - baseBounds.center) + baseBounds.center;
        }
    }

    public Vector3[] RotatedPoints
    {
        get
        {
            Vector3 extents = baseBounds.extents;
            Vector3 center = baseBounds.center;
            Vector3[] originPoints = new Vector3[]
            {
               new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z), // right-up-front
               new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z), // left-up-front
               new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z), // right-down-front
               new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z), // left-down-front
               new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z), // right-up-back
               new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z), // left-up-back
               new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z), // right-down-back
               new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z), // left-down-back
            };

            for (int i = 0; i < originPoints.Length; i++)
            {
                originPoints[i] = RotatePointAroundPivot(originPoints[i], center, rotation);
            }
            return originPoints;
        }
    }




    public Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
    {
        Vector3 dir = point - pivot;
        dir = Quaternion.Euler(angles) * dir;
        point = dir + pivot;
        return point;
    }

}
