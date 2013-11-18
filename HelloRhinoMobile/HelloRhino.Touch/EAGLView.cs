//
// EAGLView.cs
// HelloRhino.Touch
//
// Created by dan (dan@mcneel.com) on 9/19/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Timers;

using OpenTK;
using OpenTK.Platform.iPhoneOS;
using OpenTK.Graphics.ES20;

using MonoTouch.Foundation;
using MonoTouch.CoreAnimation;
using MonoTouch.ObjCRuntime;
using MonoTouch.OpenGLES;
using MonoTouch.UIKit;

using Rhino.DocObjects;
using RhinoMobile.Display;
using RhinoMobile;
using RhinoMobile.Model;

namespace HelloRhino.Touch
{

	[Register ("EAGLView")]
	public class EAGLView : iPhoneOSGameView
	{
		enum InitializationState
		{
			Uninitialized,
			Initialized,
			ErrorDuringInitialization
		}

		#region members
		//rendering fields
		InitializationState m_initialized = InitializationState.Uninitialized;
		readonly ES2Renderer m_renderer;
		CADisplayLink m_displayLink;
		ViewportInfo m_viewport;
		int m_frameBufferWidth, m_frameBufferHeight;
		int m_frameInterval;

    RhGLFramebufferObject  m_visibleFBO; // FBO created by OpenTk framework...
    RhGLFramebufferObject  m_msaaFBO;    // FBO we create for MSAA rendering...
    RhGLFramebufferObject  m_activeFBO;  // placeholder that tracks which FBO is currently in use...
		#endregion

		#region constructors
		[Export("initWithCoder:")]
		public EAGLView (NSCoder coder) : base (coder)
		{
			LayerRetainsBacking = true;
			LayerColorFormat = EAGLColorFormat.RGBA8;
			m_renderer = new ES2Renderer ();
			//ES2Renderer.TestGraphicsModes ();

			ContentScaleFactor = UIScreen.MainScreen.Scale;

			SetupGestureRecognizers ();

			// subscribe to mesh prep events in the model...
			App.Manager.CurrentModel.MeshPrep += new MeshPreparationHandler (ObserveMeshPrep);
		}
		#endregion

		#region properties
		/// <value> InactivityTimer keeps track of how long the view has not changed at all. </value>
		private Timer InactivityTimer { get; set; }

		/// <value> OrbitDollyGestureRecognizer listens for single and two-finger pan-like events. </value>
		public OrbitDollyGestureRecognizer OrbitDollyRecognizer { get; private set; }

		/// <value> ZoomRecognizer listens for pinch gestures </value>
		public UIPinchGestureRecognizer ZoomRecognizer { get; private set; }

		/// <value> IsAnimating is true if the view is currently changing. </value>
		public bool IsAnimating { get; private set; }

		/// <value> FrameInterval is backed by the displayLink frameInterval. </value>
		public int FrameInterval 
		{
			get {
				return m_frameInterval;
			}

			set {
				if (value <= 0)
					throw new ArgumentException ();
				m_frameInterval = value;
				if (IsAnimating) {
					StopAnimating ();
					StartAnimating ();
				}
			}
		}
		#endregion

		#region methods
		/// <summary>
		/// ViewDidAppear is called by CocoaTouch when the view has appeared...
		/// </summary>
		public void ViewDidAppear ()
		{
			StartInactivityTimer ();
		}

		/// <summary>
		/// StartInactivityTimer resets and restarts the inactivityTimer.
		/// </summary>
		private void StartInactivityTimer ()
		{
			if (InactivityTimer != null) {
				if (InactivityTimer.Enabled)
					InactivityTimer.Close ();
			}
		
			InactivityTimer = new System.Timers.Timer (9000);
			InactivityTimer.Elapsed += new ElapsedEventHandler (OnTimedEvent);
			InactivityTimer.AutoReset = true;
			GC.KeepAlive (InactivityTimer); // so that it doesn't get garbage collected.
			InactivityTimer.Enabled = true;
		}

		/// <summary>
		/// OnTimedEvent is called every 9 seconds (an arbitrary time) to make sure that 
		/// the graphics context is maintained when the currentModel is not being viewed.  
		/// This is to handle circumstances where the user loads and initializes a model,
		/// but the is not currently being displayed.
		/// </summary>
		private void OnTimedEvent(object source, ElapsedEventArgs e)
		{
			SetNeedsDisplay ();
		}

		/// <summary>
		/// GetLayerClass return to OpenTK the type of platform-specific layer backing.
		/// </summary>
		[Export ("layerClass")]
		public static new Class GetLayerClass ()
		{
			return iPhoneOSGameView.GetLayerClass ();
		}

		/// <summary>
		/// ConfigureLayer is called by OpenTK as part of the initialization step.
		/// </summary>
		protected override void ConfigureLayer (CAEAGLLayer eaglLayer)
		{
			eaglLayer.Opaque = true;
		}


		/// <summary>
		/// Tell opengl that we want to use OpenGL ES2 capabilities
		/// Must be done right before the frame buffer is created
		/// </summary>
		InitializationState InitializeOpenGL()
		{
			if (m_initialized == InitializationState.Uninitialized) {
				ContextRenderingApi = EAGLRenderingAPI.OpenGLES2;
				base.CreateFrameBuffer ();

				//CheckGLError ();

        // Try to create a MSAA FBO that uses 8x AA and both a color and a depth buffer...
        //
        // Note: Depending on what is supported by the hardware, this might result in a
        //       lower AA. We use 8x here for starters, but the simulators only
        //       support 4x. Since we're not sure how high certain devices may go, 8x is 
        //       probably as high as we want to go for performance and resource purposes.
				m_msaaFBO = new RhGLFramebufferObject ((int)(Size.Width * ContentScaleFactor), (int)(Size.Height * ContentScaleFactor), 8, true, true);

        // If the MSAA FBO is invalid, or the samples used is 0, then it means we
        // don't really have a MSAA FBO, so just nullify it.
        //
        // Note: RhGLFramebufferObject will successfully create an FBO using 0 samples
        //       since it dynamically scales down the sample count until the creation 
        //       succeeds...which means you can eventually arrive at an FBO that uses
        //       0 samples...so if you try to create a MSAA FBO and it results in an
        //       FBO that uses 0 samples, then you really haven't created a MSAA FBO.
        // Note2: The above situation will most likely only occur on hardware that does 
        //        not support MSAA.
        if (!m_msaaFBO.IsValid || (m_msaaFBO.ColorBuffer.SamplesUsed == 0))
        {
          m_msaaFBO.Destroy ();
          m_msaaFBO = null;
        }

				//CheckGLError ();
        
				// Create our visible FBO using the framework's framebuffer...
        //
        // OpenTk creates an FBO by default for its main rendering target. We simply 
        // just create one of our FBOs from its handle...and the rest of the FBO gets
        // filled in by our implementation.
        m_visibleFBO = new RhGLFramebufferObject( (uint)Framebuffer );
				m_visibleFBO.DepthBuffer = new RhGLRenderBuffer ((int)(Size.Width * ContentScaleFactor), (int)(Size.Height * ContentScaleFactor), All.DepthComponent16, 0);

				//CheckGLError ();

        // Note: If we do have and use a MSAA FBO, then there is no reason to create a depth buffer
        //       for the visible FBO since all we'll be doing is copying the MSAA FBO into it. This
        //       cuts down on resource usage... If we plan on using both types of FBOs at runtime,
        //       then we'll need to create a depth buffer for the visible FBO always. For not, let's
        //       only do it if we failed to create a MSAA FBO because we assume that the MSAA FBO
        //       will always be the primary rendering target.
        if (m_msaaFBO != null) 
        {
          m_activeFBO = m_msaaFBO;
        } 
        else 
        {
          m_activeFBO = m_visibleFBO;
        }

				m_frameBufferWidth = (int)(Size.Width * ContentScaleFactor);
				m_frameBufferHeight = (int)(Size.Height * ContentScaleFactor);

				MakeCurrent ();
				m_initialized = InitializationState.Initialized;

				GL.Viewport (0, 0, m_frameBufferWidth, m_frameBufferHeight);

				GL.ClearDepth (0.0f);
				GL.DepthRange (0.0f, 1.0f);
				GL.Enable (EnableCap.DepthTest);
				GL.DepthFunc (DepthFunction.Equal);
				GL.DepthMask (true);

				GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Disable (EnableCap.Dither);
				GL.Disable (EnableCap.CullFace);

				//CheckGLError ();
			}
			return m_initialized;
		}

		/// <summary>
		/// OnRenderFrame is called just before DrawFrameBuffer.
		/// </summary>
		protected override void OnRenderFrame(FrameEventArgs e)
		{
			base.OnRenderFrame (e);
			DrawFrameBuffer ();
		}

		/// <summary>
		/// DrawFrameBuffer calls into the renderer to draw the frame.
		/// </summary>
		void DrawFrameBuffer()
		{
			if(InitializeOpenGL() == InitializationState.Initialized) {
				MakeCurrent ();

				m_renderer.Frame = Frame;

				if (m_viewport == null && App.Manager.CurrentModel.IsReadyForRendering)
					SetupViewport ();

				if (!App.Manager.FastDrawing)
					m_activeFBO = m_msaaFBO;
				else
					m_activeFBO = m_visibleFBO;

        // enable our active FBO...
        m_activeFBO.Enable ();

				// render the model...
				m_renderer.RenderModel (App.Manager.CurrentModel, m_viewport);

        // copy our active FBO into our visible FBO...
        // Note: if the active and visible FBO are one in the same, then this results in a NOP.
				m_activeFBO.CopyTo (m_visibleFBO);

				SwapBuffers ();
			}
		}

		/// <summary>
		/// Find the Perspective viewport in the 3dm file and sets up the default view.
		/// </summary>
		protected void SetupViewport ()
		{
			if (App.Manager.CurrentModel == null)
				return;

			m_viewport = new ViewportInfo ();

			bool viewInitialized = false;
			int viewCount = App.Manager.CurrentModel.ModelFile.Views.Count;

			// find first perspective viewport projection in file
			if (viewCount > 0) {
				foreach (var view in App.Manager.CurrentModel.ModelFile.Views) {
					if (view.Viewport.IsPerspectiveProjection) {
						viewInitialized = true;
						m_viewport = view.Viewport;
						m_viewport.TargetPoint = view.Viewport.TargetPoint;
						break;
					}
				}
			}

			// If there isn't one, then cook up a viewport from scratch...
			if (!viewInitialized) {
				m_viewport.SetScreenPort (0, m_frameBufferWidth, 0, m_frameBufferHeight, 1, 1000);
				m_viewport.TargetPoint = new Rhino.Geometry.Point3d (0, 0, 0);
				var plane = new Rhino.Geometry.Plane (Rhino.Geometry.Point3d.Origin, new Rhino.Geometry.Vector3d (-1, -1, -1));
				m_viewport.SetCameraLocation(new Rhino.Geometry.Point3d (10, 10, 10));
				var dir = new Rhino.Geometry.Vector3d (-1, -1, -1);
				dir.Unitize ();
				m_viewport.SetCameraDirection (dir);
				m_viewport.SetCameraUp (plane.YAxis);
				m_viewport.SetFrustum (-1, 1, -1, 1, 0.1, 1000);
				m_viewport.FrustumAspect = m_viewport.ScreenPortAspect;
				m_viewport.IsPerspectiveProjection = true;
				m_viewport.Camera35mmLensLength = 50;
				if (App.Manager.CurrentModel != null) {
					if (App.Manager.CurrentModel.AllMeshes != null)
						m_viewport.DollyExtents (App.Manager.CurrentModel.AllMeshes, 1.0);
				}
			}

			// Fix up viewport values
			var cameraDir = m_viewport.CameraDirection;
			cameraDir.Unitize ();
			m_viewport.SetCameraDirection (cameraDir);

			var cameraUp = m_viewport.CameraUp;
			cameraUp.Unitize ();
			m_viewport.SetCameraUp (cameraUp);

			ResizeViewport ();

			m_renderer.Viewport = m_viewport;
		}

		/// <summary>
		/// Dynamically set the frustum based on the clipping
		/// </summary>
		protected void SetFrustum (ViewportInfo viewport, Rhino.Geometry.BoundingBox bBox)
		{
			ClippingInfo clipping = new ClippingInfo();
			clipping.bbox = bBox;
			if (ClippingPlanes.CalcClippingPlanes (viewport, clipping))
				ClippingPlanes.SetupFrustum (viewport, clipping);
		}

		/// <summary>
		/// Resizes the viewport on rotation
		/// </summary>
		protected void ResizeViewport ()
		{
			if (m_viewport == null)
				return;

			var newRectangle = Bounds;
			m_viewport.SetScreenPort (0, (int)(newRectangle.Width - 1), 0, (int)(newRectangle.Height - 1), 0, 0);

			double newPortAspect = (newRectangle.Size.Width / newRectangle.Size.Height);
			m_viewport.FrustumAspect = newPortAspect;

			if (App.Manager.CurrentModel != null)
				SetFrustum (m_viewport, App.Manager.CurrentModel.BBox);
		}
	
		/// <summary>
		/// LayoutSubviews is called on rotation events
		/// </summary>
		public override void LayoutSubviews ()
		{
			base.LayoutSubviews ();
			if (m_initialized == InitializationState.Initialized) {

				ResizeViewport ();
			
				MakeCurrent ();

				m_renderer.Resize ();

				DrawFrameBuffer ();
			}
		}

		/// <summary>
		/// DestroyFrameBuffer cleans up the framebuffer and vertexbuffer objects.
		/// </summary>
		protected override void DestroyFrameBuffer ()
		{
			if (App.Manager.CurrentModel != null) {
				MakeCurrent ();

				// Destroy all buffers on all meshes
				foreach (var obj in App.Manager.CurrentModel.DisplayObjects) {
					var mesh = obj as RhinoMobile.Display.DisplayMesh;
					if (mesh != null) {
						if (mesh.VertexBufferHandle != Globals.UNSET_HANDLE) {
							uint vbo = mesh.VertexBufferHandle;
							GL.DeleteBuffers (1, ref vbo);
							mesh.VertexBufferHandle = Globals.UNSET_HANDLE;
						}
						if (mesh.IndexBufferHandle != Globals.UNSET_HANDLE) {
							uint ibo = mesh.IndexBufferHandle;
							GL.DeleteBuffers (1, ref ibo);
							mesh.IndexBufferHandle = Globals.UNSET_HANDLE;
						}
					}
				}

				// Destroy all buffers on all transparent meshes
				foreach (var obj in App.Manager.CurrentModel.TransparentObjects) {
					var mesh = obj as RhinoMobile.Display.DisplayMesh;
					if (mesh != null) {
						if (mesh.VertexBufferHandle != Globals.UNSET_HANDLE) {
							uint vbo = mesh.VertexBufferHandle;
							GL.DeleteBuffers (1, ref vbo);
							mesh.VertexBufferHandle = Globals.UNSET_HANDLE;
						}
						if (mesh.IndexBufferHandle != Globals.UNSET_HANDLE) {
							uint ibo = mesh.IndexBufferHandle;
							GL.DeleteBuffers (1, ref ibo);
							mesh.IndexBufferHandle = Globals.UNSET_HANDLE;
						}
					}
				}

				// Destroy all the buffers
        if (m_msaaFBO != null) {
          m_msaaFBO.Handle = Globals.UNSET_HANDLE;
          m_msaaFBO = null;
        }

        if (m_visibleFBO != null) {
          m_visibleFBO.Handle = Globals.UNSET_HANDLE;
          m_visibleFBO = null;
        }
			}
			base.DestroyFrameBuffer ();
		}
		#endregion

		#region Gesture Handling methods
		/// <summary>
		/// All gesture recognizers' delegates are set in ModelView to
		/// ModelView (which is UIViewController), which conforms to the UIGestureRecognizerDelegate interface.
		/// The delegate is set in the viewDidLoad method.
		/// This view's owner (ModelView) receives the: ShouldRecognizeSimultaneouslyWithGestureRecognizer
		/// callback for each of its delegates.  
		/// </summary>
		private void SetupGestureRecognizers ()
		{
			// Pinch - Zoom
			ZoomRecognizer = new UIPinchGestureRecognizer (this, new Selector ("ZoomCameraWithGesture"));
			ZoomRecognizer.Enabled = false;
			AddGestureRecognizer (ZoomRecognizer);

			// Orbit & Dolly
			OrbitDollyRecognizer = new OrbitDollyGestureRecognizer ();
			OrbitDollyRecognizer.AddTarget (this, new Selector ("OrbitDollyCameraWithGesture"));
			OrbitDollyRecognizer.MaximumNumberOfTouches = 2;
			OrbitDollyRecognizer.Enabled = false;
			AddGestureRecognizer (OrbitDollyRecognizer);
		}

		protected void EnableAllGestureRecognizers ()
		{
			foreach (UIGestureRecognizer recognizer in GestureRecognizers)
				recognizer.Enabled = true;
		}

		protected void DisableAllGestureRecognizers ()
		{
			foreach (UIGestureRecognizer recognizer in GestureRecognizers)
				recognizer.Enabled = false;
		}

		/// <summary>
		/// ZoomCameraWithGesture is called in response to ZoomRecognizer events.
		/// </summary>
		[Export("ZoomCameraWithGesture")]
		private void ZoomCameraWithGesture (UIPinchGestureRecognizer gesture)
		{
			if (m_viewport == null)
				return;

			if (gesture.State == UIGestureRecognizerState.Began) {
				if (InactivityTimer.Enabled)
					InactivityTimer.Close ();
			}

			if (gesture.State == UIGestureRecognizerState.Changed) {
				if (gesture.NumberOfTouches > 1) {
					App.Manager.FastDrawing = true;
					System.Drawing.PointF zoomPoint = OrbitDollyRecognizer.MidpointLocation;
					m_viewport.Magnify (Bounds.Size.ToSize(), gesture.Scale, 0, zoomPoint); 
					gesture.Scale = 1.0f;
				}

				SetNeedsDisplay ();
			}

			if (gesture.State == UIGestureRecognizerState.Ended || gesture.State == UIGestureRecognizerState.Cancelled) {
				if (gesture.NumberOfTouches == 0) {
					App.Manager.FastDrawing = false;
				}

				SetNeedsDisplay ();
				StartInactivityTimer ();
			}
		}

		/// <summary>
		/// OrbitDollyCameraWithGesture is called in response to OrbitDollyRecognizer events.
		/// </summary>
		[Export("OrbitDollyCameraWithGesture")]
		private void OrbitDollyCameraWithGesture (OrbitDollyGestureRecognizer gesture)
		{
			if (m_viewport == null)
				return;

			if (gesture.State == UIGestureRecognizerState.Began) {
				if (InactivityTimer.Enabled)
					InactivityTimer.Close ();

				SetNeedsDisplay ();
			}

			if (gesture.State == UIGestureRecognizerState.Changed) {
				App.Manager.FastDrawing = true;

				if (gesture.HasSingleTouch) {
					m_viewport.GestureOrbit (Bounds.Size.ToSize(), gesture.AnchorLocation, gesture.CurrentLocation);
					gesture.AnchorLocation = gesture.CurrentLocation;
				}

				if (gesture.HasTwoTouches) {
					m_viewport.LateralPan (gesture.StartLocation, gesture.MidpointLocation);
					gesture.StartLocation = gesture.MidpointLocation;
				}

				SetNeedsDisplay ();
			}

			if (gesture.State == UIGestureRecognizerState.Ended || gesture.State == UIGestureRecognizerState.Cancelled) {
				if (gesture.NumberOfTouches == 0)
					App.Manager.FastDrawing = false;

				SetNeedsDisplay ();
				StartInactivityTimer ();
			}

		}
		#endregion

		#region DisplayLink support
		/// <summary>
		/// StartAnimating is called by DisplayLink
		/// </summary>
		public void StartAnimating ()
		{
			if (IsAnimating)
				return;

			CADisplayLink displayLink = UIScreen.MainScreen.CreateDisplayLink (this, new Selector ("drawFrame"));
			displayLink.FrameInterval = m_frameInterval;
			displayLink.AddToRunLoop (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
			this.m_displayLink = displayLink;

			IsAnimating = true;
		}

		/// <summary>
		/// StopAnimating is called by DisplayLink
		/// </summary>
		public void StopAnimating ()
		{
			if (!IsAnimating)
				return;
			m_displayLink.Invalidate ();
			m_displayLink = null;
			DestroyFrameBuffer ();
			IsAnimating = false;
		}

		/// <summary>
		/// DrawFrame is called by DisplayLink and calls OnRenderFrame in response to FrameEvents
		/// </summary>
		[Export ("drawFrame")]
		void DrawFrame ()
		{
			OnRenderFrame (new FrameEventArgs ());
		}
		#endregion

		#region Model Initialization Events
		private void ObserveMeshPrep (RMModel model, MeshPreparationProgress progress)
		{
			// Success
			if (progress.PreparationDidSucceed) {
				this.InvokeOnMainThread (delegate {
					EnableAllGestureRecognizers ();
					App.Manager.CurrentModel.MeshPrep -= new MeshPreparationHandler (ObserveMeshPrep);
				});
			}
		}
		#endregion

		#region Utilities
		/// <summary>
		/// DEBUG only.
		/// <para>Checks for outstanding GL Errors and logs them to console.</para>
		/// </summary>
		public static void CheckGLError () 
		{
			#if DEBUG 
			#if __IOS__
			var err = GL.GetError ();
			do {
				if (err != ErrorCode.NoError)
					Console.WriteLine ("GL Error: {0}", err.ToString ());
				err = GL.GetError ();
			} while ((err != ErrorCode.NoError));
			#endif
			#endif
		}
		#endregion
	
	}
}