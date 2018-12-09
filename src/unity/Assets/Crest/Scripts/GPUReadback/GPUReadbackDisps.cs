﻿using UnityEngine;

namespace Crest
{
    public class GPUReadbackDisps : GPUReadbackBase<LodDataMgrAnimWaves>, ICollProvider
    {
        PerLodData _areaData;

        static GPUReadbackDisps _instance;
        public static GPUReadbackDisps Instance
        {
            get
            {
#if !UNITY_EDITOR
                return _instance;
#else
                // Allow hot code edit/recompile in editor - re-init singleton reference.
                return _instance != null ? _instance : (_instance = FindObjectOfType<GPUReadbackDisps>());
#endif
            }
        }

        protected override bool CanUseLastTwoLODs
        {
            get
            {
                // The wave contents from the last LOD can be moved back and forth between the second-to-last LOD and it
                // results in pops if we use it
                return false;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (enabled == false)
            {
                return;
            }

            Debug.Assert(_instance == null);
            _instance = this;

            _settingsProvider = _lodComponent.Settings as SimSettingsAnimatedWaves;
        }

        #region ICollProvider
        public bool GetSamplingData(ref Rect i_displacedSamplingArea, float i_minSpatialLength, SamplingData o_samplingData)
        {
            Rect undisplacedRect = new Rect(
                i_displacedSamplingArea.xMin - OceanRenderer.Instance.MaxHorizDisplacement,
                i_displacedSamplingArea.yMin - OceanRenderer.Instance.MaxHorizDisplacement,
                i_displacedSamplingArea.width + 2f * OceanRenderer.Instance.MaxHorizDisplacement,
                i_displacedSamplingArea.height + 2f * OceanRenderer.Instance.MaxHorizDisplacement
                );
            o_samplingData._tag = GetData(undisplacedRect, i_minSpatialLength);
            return o_samplingData._tag != null;
        }

        public void ReturnSamplingData(SamplingData o_data)
        {
            o_data._tag = null;
        }

        public bool ComputeUndisplacedPosition(ref Vector3 i_worldPos, SamplingData i_samplingData, out Vector3 undisplacedWorldPos)
        {
            var lodData = i_samplingData._tag as PerLodData;

            // FPI - guess should converge to location that displaces to the target position
            Vector3 guess = i_worldPos;
            // 2 iterations was enough to get very close when chop = 1, added 2 more which should be
            // sufficient for most applications. for high chop values or really stormy conditions there may
            // be some error here. one could also terminate iteration based on the size of the error, this is
            // worth trying but is left as future work for now.
            Vector3 disp = Vector3.zero;

            for (int i = 0; i < 4 && lodData._resultData.InterpolateARGB16(ref guess, out disp); i++)
            {
                Vector3 error = guess + disp - i_worldPos;
                guess.x -= error.x;
                guess.z -= error.z;
            }

            undisplacedWorldPos = guess;
            undisplacedWorldPos.y = OceanRenderer.Instance.SeaLevel;

            return true;
        }

        public bool ComputeUndisplacedPosition(ref Vector3 i_worldPos, out Vector3 undisplacedWorldPos, float minSpatialLength)
        {
            // FPI - guess should converge to location that displaces to the target position
            Vector3 guess = i_worldPos;
            // 2 iterations was enough to get very close when chop = 1, added 2 more which should be
            // sufficient for most applications. for high chop values or really stormy conditions there may
            // be some error here. one could also terminate iteration based on the size of the error, this is
            // worth trying but is left as future work for now.
            Vector3 disp = Vector3.zero;
            for (int i = 0; i < 4 && SampleDisplacement(ref guess, out disp, minSpatialLength); i++)
            {
                Vector3 error = guess + disp - i_worldPos;
                guess.x -= error.x;
                guess.z -= error.z;
            }

            undisplacedWorldPos = guess;
            undisplacedWorldPos.y = OceanRenderer.Instance.SeaLevel;

            return true;
        }

        public bool PrewarmForSamplingArea(Rect areaXZ)
        {
            return PrewarmForSamplingArea(areaXZ, 0f);
        }

        public bool PrewarmForSamplingArea(Rect areaXZ, float minSpatialLength)
        {
            return (_areaData = GetData(areaXZ, minSpatialLength)) != null;
        }

        public bool SampleDisplacement(ref Vector3 i_worldPos, out Vector3 o_displacement)
        {
            var data = GetData(new Rect(i_worldPos.x, i_worldPos.z, 0f, 0f), 0f);
            if (data == null)
            {
                o_displacement = Vector3.zero;
                return false;
            }
            return data._resultData.InterpolateARGB16(ref i_worldPos, out o_displacement);
        }

        public bool SampleDisplacement(ref Vector3 i_worldPos, out Vector3 o_displacement, float minSpatialLength)
        {
            var data = GetData(new Rect(i_worldPos.x, i_worldPos.z, 0f, 0f), minSpatialLength);
            if (data == null)
            {
                o_displacement = Vector3.zero;
                return false;
            }
            return data._resultData.InterpolateARGB16(ref i_worldPos, out o_displacement);
        }

        public bool SampleDisplacement(ref Vector3 i_worldPos, SamplingData i_data, out Vector3 o_displacement)
        {
            var lodData = i_data._tag as PerLodData;
            if (lodData == null)
            {
                o_displacement = Vector3.zero;
                return false;
            }
            return lodData._resultData.InterpolateARGB16(ref i_worldPos, out o_displacement);
        }

        public bool SampleDisplacementInArea(ref Vector3 i_worldPos, out Vector3 o_displacement)
        {
            return _areaData._resultData.InterpolateARGB16(ref i_worldPos, out o_displacement);
        }

        public void SampleDisplacementVel(ref Vector3 i_worldPos, out Vector3 o_displacement, out bool o_displacementValid, out Vector3 o_displacementVel, out bool o_velValid, float minSpatialLength)
        {
            if (!PrewarmForSamplingArea(new Rect(i_worldPos.x, i_worldPos.z, 0f, 0f), minSpatialLength))
            {
                o_displacement = Vector3.zero;
                o_displacementValid = false;
                o_displacementVel = Vector3.zero;
                o_velValid = false;
                return;
            }

            SampleDisplacementVelInArea(ref i_worldPos, out o_displacement, out o_displacementValid, out o_displacementVel, out o_velValid);
        }

        public void SampleDisplacementVelInArea(ref Vector3 i_worldPos, out Vector3 o_displacement, out bool o_displacementValid, out Vector3 o_displacementVel, out bool o_velValid)
        {
            o_displacementValid = _areaData._resultData.InterpolateARGB16(ref i_worldPos, out o_displacement);
            if (!o_displacementValid)
            {
                o_displacementVel = Vector3.zero;
                o_velValid = false;
                return;
            }

            // Check if this lod changed scales between result and previous result - if so can't compute vel. This should
            // probably go search for the results in the other LODs but returning 0 is easiest for now and should be ok-ish
            // for physics code.
            if (_areaData._resultDataPrevFrame._renderData._texelWidth != _areaData._resultData._renderData._texelWidth)
            {
                o_displacementVel = Vector3.zero;
                o_velValid = false;
                return;
            }

            Vector3 dispLast;
            o_velValid = _areaData._resultDataPrevFrame.InterpolateARGB16(ref i_worldPos, out dispLast);
            if (!o_velValid)
            {
                o_displacementVel = Vector3.zero;
                return;
            }

            Debug.Assert(_areaData._resultData.Valid && _areaData._resultDataPrevFrame.Valid);
            o_displacementVel = (o_displacement - dispLast) / Mathf.Max(0.0001f, _areaData._resultData._time - _areaData._resultDataPrevFrame._time);
        }

        public bool SampleHeight(ref Vector3 i_worldPos, out float height)
        {
            return SampleHeight(ref i_worldPos, out height, 0f);
        }

        public bool SampleHeight(ref Vector3 i_worldPos, out float height, float minSpatialLength)
        {
            var posFlatland = i_worldPos;
            posFlatland.y = OceanRenderer.Instance.transform.position.y;

            Vector3 undisplacedPos;
            ComputeUndisplacedPosition(ref posFlatland, out undisplacedPos, minSpatialLength);

            var disp = Vector3.zero;
            SampleDisplacement(ref undisplacedPos, out disp, minSpatialLength);

            height = posFlatland.y + disp.y;
            return true;
        }

        public bool SampleHeight(ref Vector3 i_worldPos, SamplingData i_samplingData, out float height)
        {
            var posFlatland = i_worldPos;
            posFlatland.y = OceanRenderer.Instance.transform.position.y;

            Vector3 undisplacedPos;
            ComputeUndisplacedPosition(ref posFlatland, i_samplingData, out undisplacedPos);

            var disp = Vector3.zero;
            SampleDisplacement(ref undisplacedPos, i_samplingData, out disp);

            height = posFlatland.y + disp.y;
            return true;
        }

        public bool SampleNormal(ref Vector3 i_undisplacedWorldPos, out Vector3 o_normal)
        {
            return SampleNormal(ref i_undisplacedWorldPos, out o_normal, 0f);
        }

        public bool SampleNormal(ref Vector3 i_undisplacedWorldPos, out Vector3 o_normal, float minSpatialLength)
        {
            // select lod. this now has a 1 texel buffer, so the finite differences below should all be valid.
            if (!PrewarmForSamplingArea(new Rect(i_undisplacedWorldPos.x, i_undisplacedWorldPos.z, 0f, 0f), minSpatialLength))
            {
                o_normal = Vector3.zero;
                return false;
            }

            return SampleNormalInArea(ref i_undisplacedWorldPos, out o_normal);
        }

        public bool SampleNormalInArea(ref Vector3 i_undisplacedWorldPos, out Vector3 o_normal)
        {
            float gridSize = _areaData._resultData._renderData._texelWidth;
            o_normal = Vector3.zero;
            Vector3 dispCenter = Vector3.zero;
            if (!SampleDisplacementInArea(ref i_undisplacedWorldPos, out dispCenter)) return false;
            Vector3 undisplacedWorldPosX = i_undisplacedWorldPos + Vector3.right * gridSize;
            Vector3 dispX = Vector3.zero;
            if (!SampleDisplacementInArea(ref undisplacedWorldPosX, out dispX)) return false;
            Vector3 undisplacedWorldPosZ = i_undisplacedWorldPos + Vector3.forward * gridSize;
            Vector3 dispZ = Vector3.zero;
            if (!SampleDisplacementInArea(ref undisplacedWorldPosZ, out dispZ)) return false;

            o_normal = Vector3.Cross(dispZ + Vector3.forward * gridSize - dispCenter, dispX + Vector3.right * gridSize - dispCenter).normalized;

            return true;
        }

        public void SampleDisplacementVel(ref Vector3 i_worldPos, SamplingData i_samplingData, out Vector3 o_displacement, out bool o_displacementValid, out Vector3 o_displacementVel, out bool o_velValid)
        {
            var lodData = i_samplingData._tag as PerLodData;

            o_displacementValid = lodData._resultData.InterpolateARGB16(ref i_worldPos, out o_displacement);
            if (!o_displacementValid)
            {
                o_displacementVel = Vector3.zero;
                o_velValid = false;
                return;
            }

            // Check if this lod changed scales between result and previous result - if so can't compute vel. This should
            // probably go search for the results in the other LODs but returning 0 is easiest for now and should be ok-ish
            // for physics code.
            if (lodData._resultDataPrevFrame._renderData._texelWidth != lodData._resultData._renderData._texelWidth)
            {
                o_displacementVel = Vector3.zero;
                o_velValid = false;
                return;
            }

            Vector3 dispLast;
            o_velValid = lodData._resultDataPrevFrame.InterpolateARGB16(ref i_worldPos, out dispLast);
            if (!o_velValid)
            {
                o_displacementVel = Vector3.zero;
                return;
            }

            Debug.Assert(lodData._resultData.Valid && lodData._resultDataPrevFrame.Valid);
            o_displacementVel = (o_displacement - dispLast) / Mathf.Max(0.0001f, lodData._resultData._time - lodData._resultDataPrevFrame._time);
        }

        public bool SampleNormal(ref Vector3 i_undisplacedWorldPos, SamplingData i_samplingData, out Vector3 o_normal)
        {
            var lodData = i_samplingData._tag as PerLodData;
            var gridSize = lodData._resultData._renderData._texelWidth;

            o_normal = Vector3.zero;

            var dispCenter = Vector3.zero;
            if (!lodData._resultData.InterpolateARGB16(ref i_undisplacedWorldPos, out dispCenter)) return false;

            var undisplacedWorldPosX = i_undisplacedWorldPos + Vector3.right * gridSize;
            var dispX = Vector3.zero;
            if (!lodData._resultData.InterpolateARGB16(ref undisplacedWorldPosX, out dispX)) return false;

            var undisplacedWorldPosZ = i_undisplacedWorldPos + Vector3.forward * gridSize;
            var dispZ = Vector3.zero;
            if (!lodData._resultData.InterpolateARGB16(ref undisplacedWorldPosZ, out dispZ)) return false;

            o_normal = Vector3.Cross(dispZ + Vector3.forward * gridSize - dispCenter, dispX + Vector3.right * gridSize - dispCenter).normalized;

            return true;
        }
        #endregion
    }
}
