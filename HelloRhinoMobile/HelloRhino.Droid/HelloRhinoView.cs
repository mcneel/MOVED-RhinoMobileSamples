//
// HelloRhinoView.cs
// HelloRhino.Droid
//
// Created by dan (dan@mcneel.com) on 7/11/2014
// Copyright 2014 Robert McNeel & Associates.  All rights reserved.
//
using System;
using Android.Views;
using Android.Content;
using Android.Opengl;

using RhinoMobile.Display;
using Android.Widget;

#region OpenTK-1.0 API diffs
// This region handles differences between OpenTK-1.0  on MonoDroid and MonoTouch.  
// MonoDroid is behind the times and has not yet caught up with MonoTouch
// on the OpenTK-1.0 front.  Once things stabilize, this can be removed. 
// See this thread for details:
// http://forums.xamarin.com/discussion/1939/renderbuffertarget-in-opentk
#if __ANDROID__
using BufferTarget = OpenTK.Graphics.ES20.All;
using BufferUsage = OpenTK.Graphics.ES20.All;
using VertexAttribPointerType = OpenTK.Graphics.ES20.All;
using ShaderType = OpenTK.Graphics.ES20.All;
using EnableCap = OpenTK.Graphics.ES20.All;
using ProgramParameter = OpenTK.Graphics.ES20.All;
using ShaderParameter = OpenTK.Graphics.ES20.All;
using GetError = OpenTK.Graphics.ES20.All;
using Color4 = OpenTK.Graphics.ES20.All;
using DepthFunction = OpenTK.Graphics.ES20.All;
using BlendingFactorSrc = OpenTK.Graphics.ES20.All;
using BlendingFactorDest = OpenTK.Graphics.ES20.All;
using CullFaceMode = OpenTK.Graphics.ES20.All;
using FramebufferTarget = OpenTK.Graphics.ES20.All;
using RenderbufferTarget = OpenTK.Graphics.ES20.All;
using RenderbufferInternalFormat = OpenTK.Graphics.ES20.All;
using FramebufferSlot = OpenTK.Graphics.ES20.All;
#endif
#endregion

namespace HelloRhino.Droid
{
  public sealed class HelloRhinoView : GLSurfaceView
  {
    #region properties
    /// <value> Android GLSurface Views require a Renderer that conforms to GLSurfaceView.IRenderer. </value>
    private HelloRhinoAndroidRenderer AndroidRenderer { get; set; }

    /// <value> The ZoomDetector has a ZoomScaleListener which handles zooming of the Camera. </value>
    public ScaleGestureDetector ZoomDetector { get; set; }

    /// <value> The OrbitDollyDetector handles orbit and dolly (panning) gestures. </value>
    public OrbitDollyGestureDetector OrbitDollyDetector { get; set; }

    /// <value> TODO: Documentation has not yet been entered on this subject. </value>
    public GestureDetector DoubleTapDetector { get; set; }
    #endregion

    #region constructors and disposal
    public HelloRhinoView (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
    {
      // Create an OpenGL ES 2.0 context.
      SetEGLContextClientVersion (2);

      // Set the Renderer for drawing on the GLSurfaceView
      AndroidRenderer = new HelloRhinoAndroidRenderer ();
      SetRenderer (AndroidRenderer);

      // The renderer only renders when the surface is created, or when requestRender() is called.
      this.RenderMode = Rendermode.WhenDirty;

      ZoomDetector = new ScaleGestureDetector(context, new ZoomScaleListener(AndroidRenderer));
      OrbitDollyDetector = new OrbitDollyGestureDetector ();
      DoubleTapDetector = new GestureDetector (new DoubleTapListener (AndroidRenderer));
      DoubleTapDetector.SetOnDoubleTapListener(new DoubleTapListener (AndroidRenderer));


    }
    #endregion

    #region Gesture Handling methods
    /// <summary> OnTouchEvent is called for every touch event on this view </summary>
    public override bool OnTouchEvent (MotionEvent e)
    {
      if (App.Manager.CurrentModel == null || !App.Manager.CurrentModel.IsReadyForRendering)
        return false;
        
      ZoomDetector.OnTouchEvent(e);
      OrbitDollyDetector.OnTouchEvent (e);
      DoubleTapDetector.OnTouchEvent (e);
      
      if (OrbitDollyDetector.State == GestureDetectorState.Changed) {
        if (OrbitDollyDetector.HasSingleFinger) {
          AndroidRenderer.Camera.GestureOrbit (AndroidRenderer.BufferSize, OrbitDollyDetector.AnchorLocation, OrbitDollyDetector.CurrentLocation);
          OrbitDollyDetector.AnchorLocation = OrbitDollyDetector.CurrentLocation;
        }

        if (OrbitDollyDetector.HasTwoFingers) {
          AndroidRenderer.Camera.LateralPan (OrbitDollyDetector.StartLocation, OrbitDollyDetector.MidpointLocation, false, false);
          OrbitDollyDetector.StartLocation = OrbitDollyDetector.MidpointLocation;
        }
      }

      AndroidRenderer.ZoomPoint = OrbitDollyDetector.MidpointLocation;

      RequestRender ();
  
      return true;
    }
    #endregion

  }
}

