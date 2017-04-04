﻿/**
Graph Tool

Copyright (c) 2017 Sokuhatiku

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GraphTool
{
	[ExecuteInEditMode]
	public class GraphHandler : MonoBehaviour
	{
		
		#region Constant

		const float DENOMINATOR_MIN = 0.01f;
		public const int COUNT_SYSKEY = 1;
		public const int SYSKEY_TIMESTAMP = 0;

		#endregion


		#region Data

		[SerializeField] protected bool _acceptData = true;
		[SerializeField] protected bool _autoDetermine = true;
		[SerializeField] protected bool _acceptUnregisteredKey = false;

		float startTime = 0f;
		bool dataAccepted = false;

		[SerializeField, HideInInspector]
		List<Data> dataList = new List<Data>();

		public void Determine()
		{
			dataList[SYSKEY_TIMESTAMP].SetCurrent(Time.time - startTime);
			foreach (var data in dataList)
				data.Determine();
			UpdateGraph();
		}

		public void ClearAll()
		{
			foreach (var data in dataList)
				data.Clear();
			startTime = Time.time;
			scopeOffset.x = 0;
			UpdateGraph();
		}

		private void RegisterInternal(int dataKey, Data data)
		{
			if (dataKey >= dataList.Count) dataList.Insert(dataKey, data);
			else dataList[dataKey] = data;
		}

		public bool IsKeyValid(int dataKey)
		{
			return 0 <= dataKey && dataKey < dataList.Count && dataList[dataKey] != null;
		}

		public float? GetCurrentValue(int dataKey)
		{
			if (!IsKeyValid(dataKey)) return null;
			return dataList[dataKey].GetReader().CurrentValue;
		}

		public float? GetLatestValue(int dataKey)
		{
			if (!IsKeyValid(dataKey)) return null;
			return dataList[dataKey].GetReader().LatestValue;
		}

		public void SetValue(int dataKey, float value)
		{
			if (!_acceptData) return;
			if (!IsKeyValid(dataKey))
			{
				if (dataKey >= 0)
				{
					if (_acceptUnregisteredKey)
						RegisterInternal(dataKey, new Data("data " + dataKey));
					else
					{
						Debug.LogError("Attempted set value using unregistered key.");
						return;
					}
				}
				else return;
			}
			if (dataKey < COUNT_SYSKEY)
			{
				Debug.LogError("System data can not set values.");
				return;
			}

			dataList[dataKey].SetCurrent(value);
			dataAccepted = true;
		}

		public Data.Reader GetDataReader(int dataKey)
		{
			if (!IsKeyValid(dataKey))
			{
				if (dataKey >= 0 && _acceptUnregisteredKey)
					RegisterInternal(dataKey, new Data("data " + dataKey));
				else throw new ArgumentException("Data does not exist at specified key.", "dataKey");
			}

			return dataList[dataKey].GetReader();
		}

		#endregion


		#region Scope

		[SerializeField] protected Vector2 scopeOffset = new Vector2(0, 0);
		[SerializeField] protected Vector2 scopeSize = new Vector2(5f, 200f);
		[SerializeField] protected bool scopeUnsigned = false;
		[SerializeField] protected bool scopeFollowLatest = true;

		private Rect _scopeRect;
		public Rect ScopeRect
		{
			get { return _scopeRect; }
			set
			{
				if (_scopeRect != value)
				{
					_scopeRect = value;
					UpdateGraph();
				}
			}
		}

		void UpdateScopeFromProperty()
		{
			if (scopeFollowLatest)
				scopeOffset.x = GetLatestValue(SYSKEY_TIMESTAMP) ?? scopeOffset.x;
			_scopeRect = new Rect(scopeOffset, scopeSize);
			_scopeRect.x -= scopeSize.x;
			if (!scopeUnsigned) _scopeRect.y -= scopeSize.y / 2;
		}

		#endregion


		#region Grid

		[SerializeField] protected Vector2 _gridCellSize = new Vector2(1, 10);
		public Vector2 GridCellSize
		{
			get { return _gridCellSize; }
			set
			{
				_gridCellSize = new Vector2(
					Mathf.Max(value.x, DENOMINATOR_MIN),
					Mathf.Max(value.y, DENOMINATOR_MIN));
			}
		}

		[SerializeField] protected int _gridXSubdivision = 10;
		public int GridSubdivisionX
		{
			get { return _gridXSubdivision; }
			set { _gridXSubdivision = Mathf.Max(value, 1); }
		}

		[SerializeField] protected int _gridYSubdivision = 10;
		public int GridSubdivisionY
		{
			get { return _gridYSubdivision; }
			set { _gridYSubdivision = Mathf.Max(value, 1); }
		}

		void GridParameterCheck()
		{
			scopeSize = new Vector2(
				scopeSize.x < DENOMINATOR_MIN ? DENOMINATOR_MIN : scopeSize.x,
				scopeSize.y < DENOMINATOR_MIN ? DENOMINATOR_MIN : scopeSize.y);

			_gridCellSize = new Vector2(
				_gridCellSize.x < DENOMINATOR_MIN ? DENOMINATOR_MIN : _gridCellSize.x,
				_gridCellSize.y < DENOMINATOR_MIN ? DENOMINATOR_MIN : _gridCellSize.y);

			_gridXSubdivision = Mathf.Max(_gridXSubdivision, 1);
			_gridYSubdivision = Mathf.Max(_gridYSubdivision, 1);
		}

		#endregion


		#region IndexRange

		protected int _inScopeFirstIndex = -1;
		public int InScopeFirstIndex { get { return _inScopeFirstIndex; } }

		protected int _inScopeLastIndex = -1;
		public int InScopeLastIndex { get { return _inScopeLastIndex; } }

		protected void UpdateIndexRange()
		{
			var time = GetDataReader(SYSKEY_TIMESTAMP);
			_inScopeFirstIndex = _inScopeLastIndex = -1;

			if (time.Count == 0)
			{
				return;
			}
			else if (time.Count == 1)
			{
				if (ScopeRect.xMin <= time[0].Value && time[0].Value <= ScopeRect.xMax)
					_inScopeFirstIndex = _inScopeLastIndex = 0;
				return;
			}
			else
			{
				if (time[time.Count - 1].Value < ScopeRect.xMin ||
					ScopeRect.xMax < time[0].Value)
					return;
				int fmin = 0, fmax = time.Count - 1;
				int lmin = 0, lmax = fmax;
				if (ScopeRect.xMin <= time[0].Value)
					_inScopeFirstIndex = 0;
				else
				{
					for (int i = 0; i < time.Count; i++)
					{
						var index = fmin + (fmax - fmin) / 2;
						if (time[index].Value < ScopeRect.xMin)
						{
							if (ScopeRect.xMin <= time[index + 1].Value)
							{
								_inScopeFirstIndex = index;
								break;
							}
							else
							{
								fmin = index + 1;
								lmin = Mathf.Max(lmin, fmin);
							}
						}
						else
						{
							fmax = index - 1;
							if (ScopeRect.xMax < time[index].Value)
								lmax = Mathf.Min(lmax, index);
						}
					}
				}
				if (time[time.Count - 1].Value <= ScopeRect.xMax)
					_inScopeLastIndex = time.Count - 1;
				else
				{
					for (int i = 0; i < time.Count; i++)
					{
						var index = lmin + (lmax - lmin) / 2;
						if (ScopeRect.xMax < time[index].Value)
						{
							if (time[index - 1].Value <= ScopeRect.xMax)
							{
								_inScopeLastIndex = index;
								break;
							}
							else
							{
								lmax = index - 1;
							}
						}
						else
						{
							lmin = index + 1;
						}
					}
				}
			}
		}

		#endregion


		#region UpdateGraph

		public Action OnUpdateGraph;

		public void UpdateGraph()
		{
			UpdateScopeFromProperty();
			UpdateIndexRange();

			if (OnUpdateGraph != null)
				OnUpdateGraph();
		}

		#endregion


		#region UnityMessages

		private void Reset()
		{
			RegisterInternal(SYSKEY_TIMESTAMP, new Data("Timestamp"));
		}

		private void Start()
		{
			UpdateGraph();
		}

		private void LateUpdate()
		{

#if UNITY_EDITOR
			if (!UnityEditor.EditorApplication.isPlaying)
				return;
#endif

			if (_autoDetermine && dataAccepted)
				Determine();
		}

		private void OnRectTransformDimensionsChange()
		{
			UpdateGraph();
		}

		private void OnValidate()
		{
			GridParameterCheck();
			UpdateGraph();
		}

		private void OnGUI()
		{
			GUILayout.Label("データ数: " + (InScopeLastIndex - InScopeFirstIndex));
		}

		#endregion

	}
}