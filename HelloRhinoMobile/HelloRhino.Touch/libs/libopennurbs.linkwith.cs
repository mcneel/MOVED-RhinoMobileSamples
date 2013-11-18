//
// libopennurbs.linkwith.cs
// RV.Touch
//
// Created by dan (dan@mcneel.com) on 7/19/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
//
using System;
using MonoTouch.ObjCRuntime;

[assembly: LinkWith ("libopennurbs.a", LinkTarget = LinkTarget.Simulator | LinkTarget.ArmV7 | LinkTarget.ArmV7s, ForceLoad = true, IsCxx = true)]