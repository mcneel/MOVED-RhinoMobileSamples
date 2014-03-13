//
// HelloRhinoView.cs
// HelloRhino.Droid
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
using System.Drawing;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using OpenTK.Platform.Android;

using Android.Views;
using Android.Content;
using Android.Util;

using Rhino.DocObjects;
using RhinoMobile;
using RhinoMobile.Display;

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

	class HelloRhinoView : AndroidGameView
	{

		enum InitializationState
		{
			Uninitialized,
			Initialized,
			ErrorDuringInitialization
		}

		#region members
		InitializationState m_initialized = InitializationState.Uninitialized;
		ES2Renderer m_renderer;
		uint m_depth_buffer_handle = Globals.UNSET_HANDLE;
		int m_viewport_width, m_viewport_height;
		int m_frameBufferWidth, m_frameBufferHeight;

		private static readonly int InvalidPointerId = -1;
		private readonly ScaleGestureDetector m_zoomDetector;
		private int m_activePointerId = InvalidPointerId;
		private System.Drawing.PointF m_lastTouchPoint;
		private System.Drawing.PointF m_currentTouchPoint;
		private float m_lastTouchX;
		private float m_lastTouchY;
		private float m_posX;
		private float m_posY;

		public float m_scaleFactor = 1.0f;
		public ViewportInfo m_viewport;
		#endregion

		#region constructors
		public HelloRhinoView (Context context) : base (context)
		{
			m_zoomDetector = new ScaleGestureDetector(context, new ZoomScaleListener(this));
		}

		public HelloRhinoView (Context context, IAttributeSet attrs) : base (context, attrs)
		{
			m_zoomDetector = new ScaleGestureDetector(context, new ZoomScaleListener(this));
		}

		public HelloRhinoView (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{

		}
		#endregion

		#region properties
		/// <value> OpenGL ES 2.0 depth buffer handle </value>
		public uint DepthBufferHandle
		{
			get { return m_depth_buffer_handle; }
			set
			{
				if (m_depth_buffer_handle != Globals.UNSET_HANDLE && value != Globals.UNSET_HANDLE)
					throw new Exception ("Attempting to overwrite a handle");
				m_depth_buffer_handle = value;
			}
		}
		#endregion

		#region methods
		/// <summary>
		/// This method is called everytime the context needs
		/// to be recreated. Use it to set any egl-specific settings
		/// prior to context creation
		/// </summary>
		protected override void CreateFrameBuffer ()
		{
			ContextRenderingApi = GLVersion.ES2;

			// the default GraphicsMode that is set consists of (16, 16, 0, 0, 2, false)
			try {
				Log.Verbose ("GLTriangle", "Loading with default settings");
				GraphicsMode = new AndroidGraphicsMode (16, 16, 0, 0, 2, false);

				// if you don't call this, the context won't be created
				base.CreateFrameBuffer ();
				return;
			} catch (Exception ex) {
				Log.Verbose ("GLTriangle", "{0}", ex);
			}

			// this is a graphics setting that sets everything to the lowest mode possible so
			// the device returns a reliable graphics setting.
			try {
				Log.Verbose ("GLTriangle", "Loading with custom Android settings (low mode)");
				GraphicsMode = new AndroidGraphicsMode (0, 0, 0, 0, 0, false);

				// if you don't call this, the context won't be created
				base.CreateFrameBuffer ();
				return;
			} catch (Exception ex) {
				Log.Verbose ("GLTriangle", "{0}", ex);
			}
			throw new Exception ("Can't load egl, aborting");
		}

		/// <summary>
		/// <para>This gets called when the drawing surface has been created
		/// There is already a GraphicsContext and Surface at this point,
		/// following the standard OpenTK/GameWindow logic.</para>
		/// <para>
		/// Android will only render when it refreshes the surface for
		/// the first time, so if you don't call Run, you need to hook
		/// up the Resize delegate or override the OnResize event to
		/// get the updated bounds and re-call your rendering code.
		/// This will also allow non-Run-loop code to update the screen
		/// when the device is rotated.
		/// </para>
		/// </summary>
		protected override void OnLoad (EventArgs e)
		{
			// This is completely optional and only needed if you've registered delegates for OnLoad
			base.OnLoad (e);

			m_viewport_width = Width; m_viewport_height = Height;

			// Tell the model to prepare itself for display...
			if (!App.Manager.CurrentModel.IsReadyForRendering)
				App.Manager.CurrentModel.Prepare ();

			DrawFrameBuffer ();

			Run ();
		}

		/// <summary>
		/// Tell opengl that we want to use OpenGL ES2 capabilities.
		/// This must be done right before the frame buffer is created.
		/// </summary>
		InitializationState InitializeOpenGL()
		{
			if (m_initialized == InitializationState.Uninitialized) {
				m_renderer = new ES2Renderer ();
				m_renderer.AndroidContext = App.Manager.ApplicationContext;

				// Create the depth buffer...
				if (m_depth_buffer_handle == Globals.UNSET_HANDLE) {
					uint depth_buffer;
					GL.GenRenderbuffers(1, out depth_buffer);
					DepthBufferHandle = depth_buffer;
					System.Diagnostics.Debug.Assert (m_depth_buffer_handle != Globals.UNSET_HANDLE, "Cannot create m_depth_buffer_handle");
				}

				GL.BindRenderbuffer (RenderbufferTarget.Renderbuffer, DepthBufferHandle);
				GL.RenderbufferStorage (RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.DepthComponent16, Size.Width, Size.Height);
				GL.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, FramebufferSlot.DepthAttachment, RenderbufferTarget.Renderbuffer, DepthBufferHandle);

				m_frameBufferWidth = (int)(Size.Width);
				m_frameBufferHeight = (int)(Size.Height);

				// Initialize and setup some start states...
				MakeCurrent ();
				m_initialized = InitializationState.Initialized;

				GL.ClearDepth (0.0f);
				GL.DepthRange (0.0f, 1.0f);
				GL.Enable (EnableCap.DepthTest);
				GL.DepthFunc (DepthFunction.Equal);
				GL.DepthMask (true);

				GL.BlendFunc (BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Disable (EnableCap.Dither);
				GL.Disable (EnableCap.CullFace);
			}
			return m_initialized;
		}

		/// <summary>
		/// OnRenderFrame is called just before DrawFrameBuffer.
		/// </summary>
		protected override void OnRenderFrame (FrameEventArgs e)
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

				System.Drawing.RectangleF frame = new RectangleF (0, 0, m_viewport_width, m_viewport_height);

				m_renderer.Frame = frame;

				if (m_viewport == null && App.Manager.CurrentModel.IsReadyForRendering)
					SetupViewport ();

				// clear the view...
				m_renderer.ClearView ();

				// render the model...
				m_renderer.RenderModel (App.Manager.CurrentModel, m_viewport);

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

			m_viewport_width = Width; m_viewport_height = Height;

			if (m_viewport != null) {
				m_viewport.SetScreenPort (0, m_viewport_width, 0, m_viewport_height, 1, 1000);
				m_viewport.FrustumAspect = m_viewport.ScreenPortAspect;
			}

			// Adjust the viewport based on geometry changes, such as screen rotation
			GL.Viewport(0, 0, m_viewport_width, m_viewport_height);

			if (App.Manager.CurrentModel != null)
				SetFrustum (m_viewport, App.Manager.CurrentModel.BBox);
		}

		/// <summary>
		/// OnResize is called whenever Android raises the SurfaceChanged event
		/// </summary>
		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);

			if (m_initialized == InitializationState.Initialized) {
				ResizeViewport ();

				// the surface change event makes the context not be current, so be sure to make it current again
				MakeCurrent ();

				m_renderer.Resize ();

				// and draw again...
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
				uint dbo = DepthBufferHandle;
				GL.DeleteBuffers (1, ref dbo);
				DepthBufferHandle = Globals.UNSET_HANDLE;
			}
			m_initialized = InitializationState.Uninitialized;
			m_viewport = null;
			base.DestroyFrameBuffer ();
		}
		#endregion

		#region Gesture Handling methods
		/// <summary>
		/// OnTouchEvent is called for every touch event on this view
		/// </summary>
		public override bool OnTouchEvent (MotionEvent e)
		{
			m_zoomDetector.OnTouchEvent(e);

			// ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
			MotionEventActions action = e.Action & MotionEventActions.Mask;
			int pointerIndex;

			switch (action)
			{
			case MotionEventActions.Down:
				m_lastTouchX = e.GetX ();
				m_lastTouchY = e.GetY ();
				m_lastTouchPoint = new System.Drawing.PointF (m_lastTouchX, m_lastTouchY);
				m_activePointerId = e.GetPointerId(0);
				break;

			case MotionEventActions.Move:
				pointerIndex = e.FindPointerIndex (m_activePointerId);
				float x = e.GetX (pointerIndex);
				float y = e.GetY (pointerIndex);
				m_currentTouchPoint = new System.Drawing.Point ((int)x, (int)y);

				if (!m_zoomDetector.IsInProgress)
				{
					// Only move the ScaleGestureDetector isn't already processing a gesture.
					float deltaX = x - m_lastTouchX;
					float deltaY = y - m_lastTouchY;
					m_posX += deltaX;
					m_posY += deltaY;

					if (App.Manager.CurrentModel != null && App.Manager.CurrentModel.IsReadyForRendering) {
						m_viewport.GestureOrbit (this.Size, m_lastTouchPoint, m_currentTouchPoint);
					}

					m_lastTouchPoint = m_currentTouchPoint;

					Invalidate();
				}

				m_lastTouchX = x;
				m_lastTouchY = y;
				break;

			case MotionEventActions.Up:
			case MotionEventActions.Cancel:
				// We no longer need to keep track of the active pointer.
				m_activePointerId = InvalidPointerId;
				break;

			case MotionEventActions.PointerUp:
				// check to make sure that the pointer that went up is for the gesture we're tracking. 
				pointerIndex = (int) (e.Action & MotionEventActions.PointerIndexMask) >> (int) MotionEventActions.PointerIndexShift;
				int pointerId = e.GetPointerId(pointerIndex);
				if (pointerId == m_activePointerId)
				{
					// This was our active pointer going up. Choose a new action pointer and adjust accordingly
					int newPointerIndex = pointerIndex == 0 ? 1 : 0;
					m_lastTouchX = e.GetX(newPointerIndex);
					m_lastTouchY = e.GetY(newPointerIndex);
					m_activePointerId = e.GetPointerId(newPointerIndex);
				}
				break;

			}
			return true;
		}
		#endregion

	}

}