//
// ZoomScaleListener.cs
// HelloRhino.Droid
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
//
using System.Drawing;

using Android.Views;

using RhinoMobile;
using RhinoMobile.Display;

namespace HelloRhino.Droid
{
	class ZoomScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
	{
		#region members
		private readonly HelloRhinoView m_view;
		#endregion

		#region constructors
		public ZoomScaleListener(HelloRhinoView view)
		{
			m_view = view;
		}
		#endregion

		#region methods
		public override bool OnScale(ScaleGestureDetector detector)
		{
			m_view.m_scaleFactor *= detector.ScaleFactor;

			if (App.Manager.CurrentModel != null && App.Manager.CurrentModel.IsReadyForRendering) {
        m_view.m_viewport.Magnify (m_view.Size, m_view.m_scaleFactor, 0, m_view.ZoomPoint);
			}

			m_view.m_scaleFactor = 1.0f;

			return true;
		}
		#endregion

	}
}

