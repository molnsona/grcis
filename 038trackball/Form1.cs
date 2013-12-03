using System;
using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Scene3D;

namespace _038trackball
{
  public partial class Form1 : Form
  {
    /// <summary>
    /// Scene read from file.
    /// </summary>
    protected SceneBrep scene = new SceneBrep();

    /// <summary>
    /// Scene center point.
    /// </summary>
    protected Vector3 center = Vector3.Zero;

    /// <summary>
    /// Scene diameter.
    /// </summary>
    protected float diameter = 4.0f;

    /// <summary>
    /// GLControl guard flag.
    /// </summary>
    bool loaded = false;

    /// <summary>
    /// Are we allowed to use VBO?
    /// </summary>
    bool useVBO = true;

    #region OpenGL globals

    private uint[] VBOid = new uint[ 2 ];       // vertex array (colors, normals, coords), index array
    private int stride = 0;                     // stride for vertex array

    #endregion

    #region FPS counter

    long lastFpsTime = 0L;
    int frameCounter = 0;
    long triangleCounter = 0L;

    #endregion

    public Form1 ()
    {
      InitializeComponent();
      String[] tok = "$Rev$".Split( ' ' );
      Text += " (rev: " + tok[ 1 ] + ')';
    }

    private void glControl1_Load ( object sender, EventArgs e )
    {
      loaded = true;

      // OpenGL init code:
      GL.ClearColor( Color.DarkBlue );
      GL.Enable( EnableCap.DepthTest );
      GL.ShadeModel( ShadingModel.Flat );

      // VBO init:
      GL.GenBuffers( 2, VBOid );
      if ( GL.GetError() != ErrorCode.NoError )
        useVBO = false;

      SetupViewport();

      Application.Idle += new EventHandler( Application_Idle );
    }

    private void glControl1_Resize ( object sender, EventArgs e )
    {
      if ( !loaded ) return;

      SetupViewport();
      glControl1.Invalidate();
    }

    private void glControl1_Paint ( object sender, PaintEventArgs e )
    {
      Render();
    }

    private void buttonOpen_Click ( object sender, EventArgs e )
    {
      OpenFileDialog ofd = new OpenFileDialog();

      ofd.Title = "Open Scene File";
      ofd.Filter = "Wavefront OBJ Files|*.obj;*.obj.gz" +
          "|All scene types|*.obj";

      ofd.FilterIndex = 1;
      ofd.FileName = "";
      if ( ofd.ShowDialog() != DialogResult.OK )
        return;

      WavefrontObj objReader = new WavefrontObj();
      objReader.MirrorConversion = false;

      int faces = objReader.ReadBrep( ofd.FileName, scene );

      scene.BuildCornerTable();
      diameter = scene.GetDiameter( out center );
      scene.GenerateColors( 12 );
      ResetCamera();

      labelFile.Text = String.Format( "{0}: {1} faces", ofd.SafeFileName, faces );
      PrepareDataBuffers();
      glControl1.Invalidate();
    }

    /// <summary>
    /// Prepare VBO content and upload it to the GPU.
    /// </summary>
    private void PrepareDataBuffers ()
    {
      if ( useVBO &&
           scene != null &&
           scene.Triangles > 0 )
      {
        GL.EnableClientState( ArrayCap.VertexArray );
        if ( scene.Normals > 0 )
          GL.EnableClientState( ArrayCap.NormalArray );
        GL.EnableClientState( ArrayCap.ColorArray );

        // Vertex array: color [normal] coord
        GL.BindBuffer( BufferTarget.ArrayBuffer, VBOid[ 0 ] );
        int vertexBufferSize = scene.VertexBufferSize( true, false, true, true );
        GL.BufferData( BufferTarget.ArrayBuffer, (IntPtr)vertexBufferSize, IntPtr.Zero, BufferUsageHint.StaticDraw );
        IntPtr videoMemoryPtr = GL.MapBuffer( BufferTarget.ArrayBuffer, BufferAccess.WriteOnly );
        unsafe
        {
          stride = scene.FillVertexBuffer( (float*)videoMemoryPtr.ToPointer(), true, false, true, true );
        }
        GL.UnmapBuffer( BufferTarget.ArrayBuffer );
        GL.BindBuffer( BufferTarget.ArrayBuffer, 0 );

        // Index buffer
        GL.BindBuffer( BufferTarget.ElementArrayBuffer, VBOid[ 1 ] );
        GL.BufferData( BufferTarget.ElementArrayBuffer, (IntPtr)(scene.Triangles * 3 * sizeof( uint )), IntPtr.Zero, BufferUsageHint.StaticDraw );
        videoMemoryPtr = GL.MapBuffer( BufferTarget.ElementArrayBuffer, BufferAccess.WriteOnly );
        unsafe
        {
          scene.FillIndexBuffer( (uint*)videoMemoryPtr.ToPointer() );
        }
        GL.UnmapBuffer( BufferTarget.ElementArrayBuffer );
        GL.BindBuffer( BufferTarget.ElementArrayBuffer, 0 );
      }
      else
      {
        GL.DisableClientState( ArrayCap.VertexArray );
        GL.DisableClientState( ArrayCap.NormalArray );
        GL.DisableClientState( ArrayCap.ColorArray );

        if ( useVBO )
        {
          GL.BindBuffer( BufferTarget.ArrayBuffer, VBOid[ 0 ] );
          GL.BufferData( BufferTarget.ArrayBuffer, (IntPtr)0, IntPtr.Zero, BufferUsageHint.StaticDraw );
          GL.BindBuffer( BufferTarget.ArrayBuffer, 0 );
          GL.BindBuffer( BufferTarget.ElementArrayBuffer, VBOid[ 1 ] );
          GL.BufferData( BufferTarget.ElementArrayBuffer, (IntPtr)0, IntPtr.Zero, BufferUsageHint.StaticDraw );
          GL.BindBuffer( BufferTarget.ElementArrayBuffer, 0 );
        }
      }
    }

    private void buttonGenerate_Click ( object sender, EventArgs e )
    {
      Cursor.Current = Cursors.WaitCursor;

      scene.Reset();
      Construction cn = new Construction();

      Matrix4 translation;
      Matrix4.CreateTranslation( 1.0f, 0.0f, 0.0f, out translation );
      Matrix4 rotation;
      Matrix4.CreateRotationX( 90.0f, out rotation );

      int faces = cn.AddMesh( scene, translation * rotation, textParam.Text );
      scene.BuildCornerTable();

      int errors = scene.CheckCornerTable( null );

      diameter = scene.GetDiameter( out center );
      scene.GenerateColors( 12 );
      ResetCamera();

      labelFile.Text = String.Format( "{0} faces, {1} errors", faces, errors );
      PrepareDataBuffers();
      glControl1.Invalidate();
      
      Cursor.Current = Cursors.Default;
    }
  }
}
