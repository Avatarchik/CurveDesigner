﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//A class which defines a chain of 3rd order beizer curves (4 control points per segment)
[System.Serializable]
public class BeizerCurve
{
    [SerializeField]
    [HideInInspector]
    public List<PointGroup> PointGroups;
    public int NumControlPoints { get { return PointGroups.Count*3-2; } }
    public int NumSegments { get { return PointGroups.Count-1; } }
    public bool placeLockedPoints = true;
    public bool isCurveOutOfDate = true;
    public SplitInsertionNeighborModification splitInsertionBehaviour;
    public List<SampleFragment> cachedFragments = null;
    [SerializeField]
    [HideInInspector]
    private List<float> _lengths = null;
    //Cummulative lengths, where index 0 is the length of the 0th item, index 1  is the length of the 0th+1st etc.
    [SerializeField]
    [HideInInspector]
    private List<float> _cummulativeLengths = null;
    
    public BeizerCurve() { }
    public BeizerCurve(BeizerCurve curveToClone)
    {
        PointGroups = new List<PointGroup>();
        foreach (var i in curveToClone.PointGroups)
        {
            PointGroups.Add(new PointGroup(i));
        }
        this._lengths = new List<float>(curveToClone._lengths);
        this.placeLockedPoints = curveToClone.placeLockedPoints;
        this.isCurveOutOfDate = curveToClone.isCurveOutOfDate;
        this.splitInsertionBehaviour = curveToClone.splitInsertionBehaviour;
    }

    public enum SplitInsertionNeighborModification
    {
        DoNotModifyNeighbors=0,
        RetainCurveShape=1,
    }

    #region curve manipulation
    public void Initialize()
    {
        PointGroups = new List<PointGroup>();
        var pointA = new PointGroup(placeLockedPoints);
        pointA.SetWorldPositionByIndex(PGIndex.Position, Vector3.zero);
        pointA.SetWorldPositionByIndex(PGIndex.RightTangent, new Vector3(1,0,0));
        PointGroups.Add(pointA);
        var pointB = new PointGroup(placeLockedPoints);
        pointB.SetWorldPositionByIndex(PGIndex.Position, new Vector3(1,1,0));
        pointB.SetWorldPositionByIndex(PGIndex.LeftTangent, new Vector3(0,1,0));
        PointGroups.Add(pointB);
    }

    public int InsertSegmentAfterIndex(CurveSplitPointInfo splitPoint)
    {
        var prePointGroup = PointGroups[splitPoint.segmentIndex];
        var postPointGroup = PointGroups[splitPoint.segmentIndex + 1];
        PointGroup point = new PointGroup(placeLockedPoints);
        var basePosition = this.GetSegmentPositionAtTime(splitPoint.segmentIndex, splitPoint.time);
        point.SetWorldPositionByIndex(PGIndex.Position,basePosition);
        Vector3 leftTangent;
        Vector3 rightTangent;
        Vector3 preLeftTangent;
        Vector3 postRightTangent;
        SolvePositionAtTimeTangents(GetVirtualIndex(splitPoint.segmentIndex, 0), 4, splitPoint.time, out leftTangent, out rightTangent, out preLeftTangent, out postRightTangent);

        void prePointModify()
        {
            prePointGroup.SetWorldPositionByIndex(PGIndex.RightTangent,preLeftTangent);
        }
        void postPointModify()
        {
            postPointGroup.SetWorldPositionByIndex(PGIndex.LeftTangent,postRightTangent);
        }
        switch (splitInsertionBehaviour)
        {
            case SplitInsertionNeighborModification.RetainCurveShape:
                prePointGroup.SetPointLocked(false);
                postPointGroup.SetPointLocked(false);
                prePointModify();
                postPointModify();
                break;
            default:
                break;
        }

        //use the bigger tangent, this only matters if the point is locked
        if ((leftTangent-point.GetWorldPositionByIndex(PGIndex.Position)).magnitude<(rightTangent-point.GetWorldPositionByIndex(PGIndex.Position)).magnitude)
        {
            point.SetWorldPositionByIndex(PGIndex.LeftTangent, leftTangent);
            point.SetWorldPositionByIndex(PGIndex.RightTangent, rightTangent);
        }
        else
        {
            point.SetWorldPositionByIndex(PGIndex.RightTangent, rightTangent);
            point.SetWorldPositionByIndex(PGIndex.LeftTangent, leftTangent);
        }

        PointGroups.Insert(splitPoint.segmentIndex+1,point);
        return (splitPoint.segmentIndex+1)*3;
    }

    public void AddDefaultSegment()
    {
        var finalPointGroup = PointGroups[PointGroups.Count - 1];
        var finalPointPos = finalPointGroup.GetWorldPositionByIndex(PGIndex.Position);
        finalPointGroup.SetWorldPositionByIndex(PGIndex.RightTangent,finalPointPos+new Vector3(1,0,0));
        var pointB = new PointGroup(placeLockedPoints);
        pointB.SetWorldPositionByIndex(PGIndex.Position,finalPointPos+new Vector3(1,1,0));
        pointB.SetWorldPositionByIndex(PGIndex.LeftTangent,finalPointPos+new Vector3(0,1,0));
        PointGroups.Add(pointB);
    }
    #endregion

    #region curve calculations
    //.private const float samplesPerUnit = 100.0f;
    private const int MaxSamples = 500;
    private const int samplesPerSegment = 10;
    private float GetAutoCurveDensity(float curveLength)
    {
        return Mathf.Max(curveLength/MaxSamples,curveLength/(samplesPerSegment*NumSegments));
    }
    public float GetDistanceBySegmentIndexAndTime(int segmentIndex,float time)
    {
        if (segmentIndex == 0)
            return time * _lengths[0];
        return _cummulativeLengths[segmentIndex - 1] + time * _lengths[segmentIndex];//this is linearly interpolating along by time which as we know isn't correct because beizers aren't linear like this...
    }
    public int GetSegmentIndexAndTimeByDistance(float distance, out float time)
    {
        for (int i = 0; i < _cummulativeLengths.Count; i++)
        {
            if (_cummulativeLengths[i] > distance)
            {
                if (i > 0)
                {
                    distance = (distance - _cummulativeLengths[i - 1]);
                }
                time = distance/_lengths[i];
                return i;
            }
        }
        //at the end of the curve
        if (_lengths.Count>1)
            distance = distance - _cummulativeLengths[_lengths.Count - 2];
        time = distance / _lengths[_lengths.Count - 1];
        return _lengths.Count - 1;
    }
    public List<SampleFragment> GetCachedSampled(float? density=null)
    {
        if (cachedFragments==null)
        {
            CacheSampleCurve(density);
        }
        return cachedFragments;
    }
    public void CacheSampleCurve(float? density=null)
    {
        CacheLengths();
        float sampleDistance;
        if (density.HasValue)
            sampleDistance = density.Value;
        else
            sampleDistance = GetAutoCurveDensity(GetLength());
        List<SampleFragment> retr = new List<SampleFragment>();
        float time;
        Vector3 previousPosition=GetPositionAtDistance(0,out time);//get start of curve point
        Vector3 position;
        float actualDistanceAlongCurve = 0.0f;
        float lenSoFar = 0;
        for (int i = 0; i < NumSegments; i++)
        {
            float f = lenSoFar;
            float segmentLength = _lengths[i];
            lenSoFar += segmentLength;
            int numSteps = Mathf.Max(1, Mathf.RoundToInt(segmentLength / sampleDistance));
            float jumpDist = segmentLength / numSteps;
            for (int j = 0; j < numSteps; j++)
            {
                position = GetPositionAtDistance(f,out time);
                actualDistanceAlongCurve += Vector3.Distance(position,previousPosition);
                previousPosition = position;
                retr.Add(new SampleFragment(position,i,time,actualDistanceAlongCurve));
                f += jumpDist;
            }
        }
        position = GetPositionAtDistance(lenSoFar,out time);
        actualDistanceAlongCurve += Vector3.Distance(position, previousPosition);
        retr.Add(new SampleFragment(position,NumSegments,time,actualDistanceAlongCurve));//add last point
        cachedFragments = retr;
    }

    //Doesn't actually sample at distance along the beizer, but rather the position at time=distance/length, which isn't quite uniform

    public Vector3 GetPositionAtDistance(float distance, out float time)
    {
        for (int i=0;i<NumSegments;i++)
        {
            if (distance-_lengths[i]<0)
            {
                time = distance / _lengths[i];
                return GetSegmentPositionAtTime(i,time);
            }
            else
            {
                distance -= _lengths[i];
            }
        }
        int finalSegmentIndex = NumSegments - 1;
        time = 1.0f;
        return GetSegmentPositionAtTime(finalSegmentIndex,time);
    }

    private void SolvePositionAtTimeTangents(int startIndex, int length, float time, out Vector3 leftTangent, out Vector3 rightTangent, out Vector3 preLeftTangent, out Vector3 postRightTangent)
    {
        leftTangent = SolvePositionAtTime(startIndex,length-1,time);
        rightTangent = SolvePositionAtTime(startIndex+1,length-1,time);

        preLeftTangent = SolvePositionAtTime(startIndex,length-2,time);
        postRightTangent = SolvePositionAtTime(startIndex+2,length-2,time);
    }

    public Vector3 GetSegmentPositionAtTime(int segmentIndex,float time)
    {
        return SolvePositionAtTime(GetVirtualIndex(segmentIndex,0),4,time);
    }

    private Vector3 SolvePositionAtTime(int startIndex, int length, float time)
    {
        if (length == 2)
            return Vector3.Lerp(this[startIndex], this[startIndex + 1], time);
        Vector3 firstHalf = SolvePositionAtTime(startIndex, length - 1, time);
        Vector3 secondHalf = SolvePositionAtTime(startIndex + 1, length - 1, time);
        return Vector3.Lerp(firstHalf, secondHalf, time);
    }
    #endregion

    #region point locking
    public void SetPointLockState(int segmentIndex, int pointIndex,bool state)
    {
        SetPointLockState(GetVirtualIndex(segmentIndex,pointIndex),state);
    }
    public bool GetPointLockState(int segmentIndex, int pointIndex)
    {
        return GetPointLockState(GetVirtualIndex(segmentIndex,pointIndex));
    }
    public void SetPointLockState(int index,bool state)
    {
        PointGroups[GetPointGroupIndex(index)].SetPointLocked(state);
    }
    public bool GetPointLockState(int index)
    {
        return PointGroups[GetPointGroupIndex(index)].GetIsPointLocked();
    }
    #endregion

    #region length calculation
    public float GetSegmentLength(int segmentIndex)
    {
        return _lengths[segmentIndex];
    }
    public float GetCummulativeSegmentLength(int segmentIndex)
    {
        return _cummulativeLengths[segmentIndex];
    }
    public float GetLength()
    {
        return _cummulativeLengths[NumSegments - 1];
    }
    public void CacheLengths()
    {
        if (_lengths==null)
            _lengths = new List<float>();
        else
            _lengths.Clear();
        for (int i = 0; i < NumSegments; i++)
        {
            _lengths.Add(CalculateSegmentLength(i));
        }
        CalculateCummulativeLengths();
    }
    public void CacheSegmentLength(int index)
    {
        if (index < 0 || index >= NumSegments)
            throw new System.ArgumentOutOfRangeException();
        if (index >= _lengths.Count)
        {
            CacheLengths();
        } else
        {
            _lengths[index] = CalculateSegmentLength(index);
            CalculateCummulativeLengths();
        }
    }
    private void CalculateCummulativeLengths()
    {
        if (_cummulativeLengths == null)
            _cummulativeLengths = new List<float>();
        else
            _cummulativeLengths.Clear();
        float len = 0;
        for (int i = 0; i < NumSegments; i++)
        {
            len += _lengths[i];
            _cummulativeLengths.Add(len);
        }
    }
    private const int _numSegmentLengthSamples = 100;
    private float CalculateSegmentLength(int segmentIndex)
    {
        float len = 0;
        Vector3 previousPosition = GetSegmentPositionAtTime(segmentIndex,0.0f);
        for (int i = 1; i < _numSegmentLengthSamples; i++)
        {
            Vector3 currentPosition = GetSegmentPositionAtTime(segmentIndex, i / (float)_numSegmentLengthSamples);
            len += Vector3.Distance(currentPosition, previousPosition);
            previousPosition = currentPosition; 
        }
        return len;
    }
    #endregion

    #region point manipulation
    public Vector3 this[int virtualIndex]
    {
        get
        {
            return GetPointGroupByIndex(virtualIndex).GetWorldPositionByIndex(GetPointTypeByIndex(virtualIndex));
        }
        set
        {
            GetPointGroupByIndex(virtualIndex).SetWorldPositionByIndex(GetPointTypeByIndex(virtualIndex),value);
        }
    }
    public Vector3 this[int segmentVirtualIndex,int pointVirtualIndex]
    {
        get
        {
            int index = GetVirtualIndex(segmentVirtualIndex, pointVirtualIndex);
            return this[index];
        }
        set
        {
            int index = GetVirtualIndex(segmentVirtualIndex,pointVirtualIndex);
            this[index] = value;
        }
    }
    #endregion

    public static PGIndex GetPointTypeByIndex(int virtualIndex)
    {
        int offsetIndex = virtualIndex-GetParentVirtualIndex(virtualIndex);
        return (PGIndex)offsetIndex;
    }
    public PGIndex GetOtherTangentIndex(PGIndex index)
    {
        switch (index)
        {
            case PGIndex.LeftTangent:
                return PGIndex.RightTangent;
            case PGIndex.RightTangent:
                return PGIndex.LeftTangent;
            case PGIndex.Position:
                return PGIndex.Position;
            default:
                throw new System.InvalidOperationException();
        }
    }
    public PointGroup GetPointGroupByIndex(int virtualIndex)
    {
        return PointGroups[GetPointGroupIndex(virtualIndex)];
    }

    public static int GetVirtualIndexByType(int parentPointGroupIndex,PGIndex type)
    {
        int retrVal = parentPointGroupIndex * 3;
        if (type== PGIndex.LeftTangent)
        {
            return retrVal - 1;
        } else if (type == PGIndex.RightTangent)
        {
            return retrVal+ 1;
        }
        return retrVal;
    }

    public static int GetVirtualIndex(int segmentIndex,int pointIndex) { return segmentIndex * 3 + pointIndex; }
    public static int GetParentVirtualIndex(int childVirtualIndex) { return GetPointGroupIndex(childVirtualIndex) * 3; }
    public static int GetPointGroupIndex(int childIndex) { return ((childIndex + 1) / 3); }
}
