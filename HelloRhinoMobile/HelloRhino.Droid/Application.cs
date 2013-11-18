//
// Application.cs
// HelloRhino.Droid
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
//
using System;

using Android.App;

using RhinoMobile;

namespace HelloRhino.Droid
{
	[Application]
	public class MainApp : Application {

		/// <summary>
		/// Overriding requires at least one non-default constructor
		/// </summary>
		public MainApp(IntPtr javaReference,  Android.Runtime.JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		public override void OnCreate ()
		{
			base.OnCreate ();

			RhinoMobile.App.Manager.ApplicationContext = base.ApplicationContext;

			RhinoMobile.App.Manager.Setup ();
		}

	}
}

