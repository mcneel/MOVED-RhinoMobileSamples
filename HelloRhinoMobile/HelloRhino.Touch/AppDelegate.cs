//
// AppDelegate.cs
// HelloRhino.Touch
//
// Created by dan (dan@mcneel.com) on 9/18/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace HelloRhino.Touch
{
	/// <summary>
	/// The UIApplicationDelegate for the application. This class is responsible for launching the 
	/// User Interface of the application, as well as listening (and optionally responding) to 
	/// application events from iOS.
	/// </summary>
	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate
	{
		#region properties
		public override UIWindow Window { get; set; }
		#endregion

		#region methods
		/// <summary>
		/// FinishedLaunching is invoked when the application has loaded its UI and is ready to run
		/// </summary>
		public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
		{
			App.Manager.Setup ();

			return true;
		}
		#endregion

	}
}