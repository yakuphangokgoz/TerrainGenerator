using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections.Specialized;
using System.Windows;

using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TerrainGenerator
{


    public partial class Form1 : Form
    {

        Query query;
        public Form1()
        {
            
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

            InitializeComponent();
            InitializeGraphics();
            InitializeEventHandler();
            query = new Query();


            //Console.WriteLine(query.long2tilex(32, 12) + " - " + query.lat2tiley(39, 12)) ;
            
            /*
            for (int i = 0; i < 90; i++) {
                try
                {
                    Console.WriteLine("Sperical Distance at : " + i + " - " + query.FindDistance(i, 30f, i, 31f));
                    Console.WriteLine("Elliptic Distance at : " + i + " - " + query.FindEllipticDistance(i, 30f, i, 31f));
                }
                catch (Exception ex) {
                    Console.WriteLine("Couldnt calculate: " + i );
                }


            }*/

            /*
            Console.WriteLine("Diagonal Spherical Distance:" + query.FindDistance(39.1f, 32.1f, 39.5f, 32.5f));
            Console.WriteLine("Latitudal Spherical Distance:" + query.FindDistance(39.1f, 32.1f, 39.5f, 32.1f));
            Console.WriteLine("Longitudal Spherical Distance:" + query.FindDistance(39.1f, 32.1f, 39.1f, 32.5f));

            Console.WriteLine("Diagonal Elliptic Distance:" + query.FindEllipticDistance(39.1f, 32.1f, 39.5f, 32.5f));
            Console.WriteLine("Latitudal Elliptic Distance:" + query.FindEllipticDistance(39.1f, 32.1f, 39.5f, 32.1f));
            Console.WriteLine("Longitudal Elliptic Distance:" + query.FindEllipticDistance(39.1f, 32.1f, 39.1f, 32.5f));
            */
            //this.Cursor = new Cursor("Mycursor.cur");
        }

        private Device device = null;
        private VertexBuffer vertexBuffer = null;
        private IndexBuffer indexBuffer = null;

        private static int terrainWidth = 69;
        private static int terrainLength = 69;
        private float camMoveSpeed = 0.2f;
        private float camTurnSpeed = 0.05f;
        private float camRotX = 0;
        private float camRotY = 0.5f;

        PresentParameters presentParams;


        private static int vertCount = terrainWidth * terrainLength;
        private static int indCount = (terrainWidth - 1) * (terrainLength - 1) * 6;

        private Vector3 camPosition, camLookAt, camUp;



        private CustomVertex.PositionNormalTextured[] verts = null;
        private static int[] indices;

        private FillMode fillMode = FillMode.WireFrame;
        private Color backgroundColor = Color.FromArgb(255, 50, 50, 50);
        private bool invalidating = true;

        private Bitmap heightMap = null;

        Dictionary<string, IList<Dictionary<string, string>>> locationData;

        Texture terrainTexture;


        private void GenerateVertices()
        {
            terrainWidth = 64;
            terrainLength = 64;
            verts = new CustomVertex.PositionNormalTextured[vertCount];

            for (int i = 0; i < terrainWidth; i++)
            {
                for (int j = 0; j < terrainLength; j++)
                {
                    //verts[(i * terrainWidth) + j] = new CustomVertex.PositionNormalTextured(j, 0, i, Color.White.ToArgb());
                    verts[(i * terrainWidth) + j] = new CustomVertex.PositionNormalTextured(j, 0, i, 0, 1, 0, 0 ,0);
                }
            }

            
        }

        int indexIndex = 0;
        private void GenerateIndices(int width, int length, int quantity)
        {
            indexIndex = 0;
            indices = new int[quantity];
            
            for (int i = 0; i < width; i++)
            {
                if (i == width - 1)
                {
                    continue;
                }
                for (int j = 0; j < length; j++)
                {
                    if (j % length == length - 1)
                    {
                        continue;
                    }

                    indices[indexIndex++] = (i * width) + j;
                    indices[indexIndex++] = (i * width) + j + 1;
                    indices[indexIndex++] = (i * width) + j + width + 1;

                    indices[indexIndex++] = (i * width) + j;
                    indices[indexIndex++] = (i * width) + j + width;
                    indices[indexIndex++] = (i * width) + j + width + 1;
                    


                }
            }

        }


        private void LoadHeightMap()
        {
            verts = new CustomVertex.PositionNormalTextured[vertCount];

            using (OpenFileDialog ofd = new OpenFileDialog()) {

                ofd.Title = "Load Heightmap";
                ofd.Filter = "Bitmap files (*.bmp)|*.bmp";
                 
                if (ofd.ShowDialog(this) == DialogResult.OK) {

                    heightMap = new Bitmap(ofd.FileName);
                    
                    Color pixelColor;
                    for (int i = 0; i < terrainWidth; i++)
                    {
                        for (int j = 0; j < terrainLength; j++)
                        {
                            pixelColor = heightMap.GetPixel(i, j);
                            //verts[(i * terrainWidth) + j] = new CustomVertex.PositionNormalTextured(j, (float)pixelColor.B/25, i, pixelColor.ToArgb());
                            verts[(i * terrainWidth) + j] = new CustomVertex.PositionNormalTextured(j, (float)pixelColor.B / 25, i, 0, 1, 0, 0,0);
                        }
                    }

                }
            }

        }

        private void ShowProgressPanel(bool isTrue) {
            this.panel4.Visible = isTrue;
            this.label5.Visible = isTrue;
            this.label6.Visible = isTrue;
            if (isTrue) {
                this.label5.Refresh();
                this.label6.Refresh();
                this.label7.Refresh();

            }
            this.progressBar1.Visible = isTrue;


        }



        string logFile = "logfile.txt";
        StreamWriter sw;

        int terrainDensity = 85;
        private void GenerateCoordinateTerrain(float minLat, float minLon, float maxLat, float maxLon) {
            int width = (maxLat - minLat != 0) ? (int)((maxLat-minLat)*terrainDensity) : 2;
            int length = (maxLon - minLon != 0) ? (int)((maxLon - minLon)*terrainDensity) : 2;
            int coordinateVertCount = width * length;
            float latitudeIncreaseFactor = (maxLat - minLat)/width;
            float longitudeIncreaseFactor = (maxLon - minLon)/length;
            vertCount = coordinateVertCount;
            verts = new CustomVertex.PositionNormalTextured[coordinateVertCount];
            
            Console.WriteLine("width: " + width + ", length: " + length);
            float queryResult = 0;
            
            this.progressBar1.Minimum = 0;
            this.progressBar1.Maximum = coordinateVertCount;
            this.progressBar1.Value = 0;
            this.progressBar1.Step = 1;

            float u = 0;
            float v = 1;

            float uFactor = 1f / width;
            float vFactor = 1f / length;

            
            sw = File.CreateText(logFile); 
            for (int i = 0; i < width; i++)
            {
               

                for (int j = 0; j < length; j++)
                {
                    
                    try
                    {
                        queryResult = float.Parse(query.MakeQuery((minLat + j * latitudeIncreaseFactor).ToString("F6").Replace(",", "."), (minLon + i * longitudeIncreaseFactor).ToString("F6").Replace(",", "."), locationData));

                        //verts[(i * width) + j] = new CustomVertex.PositionNormalTextured(j, queryResult/ 90, i, Color.White.ToArgb());
                        //verts[(i * width) + j] = new CustomVertex.PositionNormalTextured(j/4f, queryResult /300, i/4f, 0, 1, 0, (u + uFactor*i), (v - vFactor*j));
                        verts[(i * width) + j].Position = new Vector3(j, queryResult / 100,i);

                        verts[(i * width) + j].Tu = (u + uFactor * i);
                        verts[(i * width) + j].Tv = (v - vFactor * j);
                        
                       
                       

                        this.progressBar1.PerformStep();


                        sw.WriteLine("Latitude: " + (minLat + j * latitudeIncreaseFactor).ToString("F6").Replace(",", ".") +
                            ", Longitude: " + (minLon + i * longitudeIncreaseFactor).ToString("F6").Replace(",", ".") +
                            ", Elevation: " + queryResult + " meters\n");


                        /*this.label7.Text += "Latitude: " + (minLat + j * latitudeIncreaseFactor).ToString("F6").Replace(",", ".") + 
                            ", Longitude: " + (minLon + i * longitudeIncreaseFactor).ToString("F6").Replace(",", ".") + 
                            ", Elevation: " + queryResult + " meters\n" ;*/


                    }

                    catch (Exception ex) {
                        Console.WriteLine((minLat + j * latitudeIncreaseFactor).ToString("F6") + "---" + (minLon + i * longitudeIncreaseFactor).ToString("F6"));
                        Console.WriteLine(ex.Message);
                        return;
                    }
                }
                
            }
            sw.Close();

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < length; j++)
                {

                    if (i == 0 && j == 0)
                    {
                        verts[(i * width) + j].Normal = Vector3.Cross(verts[(i * width) + j + 1].Position - verts[(i * width) + j].Position,
                            verts[((i + 1) * width) + j].Position - verts[(i * width) + j].Position);
                    }

                    else if (i == 0 && j == length - 1)
                    {
                        verts[(i * width) + j].Normal = Vector3.Cross(verts[((i + 1) * width) + j].Position - verts[(i * width) + j].Position,
                            verts[(i * width) + j - 1].Position - verts[(i * width) + j].Position);
                    }

                    else if (i == width - 1 && j == 0)
                    {
                        verts[(i * width) + j].Normal = Vector3.Cross(verts[((i - 1) * width) + j].Position - verts[(i * width) + j].Position,
                            verts[(i * width) + j + 1].Position - verts[(i * width) + j].Position);
                    }

                    else if (i == width - 1 && j == length - 1)
                    {
                        verts[(i * width) + j].Normal = Vector3.Cross(verts[(i * width) + j - 1].Position - verts[(i * width) + j].Position,
                            verts[((i - 1) * width) + j].Position - verts[(i * width) + j].Position);
                    }

                    else if (i == 0)
                    {

                        verts[(i * width) + j].Normal = Vector3.Cross(verts[(i * width) + j + 1].Position - verts[(i * width) + j - 1].Position,
                            verts[((i + 1) * width) + j].Position - verts[(i * width) + j].Position);

                    }

                    else if (j == 0)
                    {

                        verts[(i * width) + j].Normal = Vector3.Cross(verts[((i - 1) * width) + j].Position - verts[((i + 1) * width) + j].Position,
                            verts[(i * width) + j + 1].Position - verts[(i * width) + j].Position);
                    }

                    else if (j == length - 1)
                    {

                        verts[(i * width) + j].Normal = Vector3.Cross(verts[((i + 1) * width) + j].Position - verts[((i - 1) * width) + j].Position,
                            verts[(i * width) + j - 1].Position - verts[(i * width) + j].Position);
                    }

                    else if (i == width - 1)
                    {

                        verts[(i * width) + j].Normal = Vector3.Cross(verts[(i * width) + j - 1].Position - verts[(i * width) + j + 1].Position,
                            verts[((i - 1) * width) + j].Position - verts[(i * width) + j].Position);

                    }


                    else {

                        verts[(i * width) + j].Normal = Vector3.Cross(verts[(i * width) + j + 1].Position - verts[(i * width) + j - 1].Position,
                           verts[((i + 1) * width) + j].Position - verts[((i-1) * width) + j].Position);

                    }


                }




                
            }

            int indiceQuantity = (width - 1) * (length - 1) * 6;
            indCount = indiceQuantity;

            UpdateBuffers(vertCount, indCount); //IMPORTANT

            GenerateIndices(width, length, indiceQuantity);
            
            Console.WriteLine("Generation done");
            /*Green-Brown Scale : Color.FromArgb(255, 40, (int)((1/(queryResult* queryResult)) * 100000000 - 30)  ,0).ToArgb()*/


        }

        bool downloadStatus = false;
        bool isTextureSet = false;

        private bool DownloadHandler(float minLat, float minLon, float maxLat, float maxLon) {

            float latAverage = (maxLat + minLat) / 2;
            float lonAverage = (maxLon + minLon) / 2;

            int zoom = query.ZoomAmountFinder(minLon, maxLon) + 1;
            int query1X = query.long2tilex((double)minLon, zoom);
            int query1Y = query.lat2tiley((double)minLat, zoom);

            int query3X = query.long2tilex((double)minLon, zoom);
            int query3Y = query.lat2tiley((double)latAverage, zoom);

            int query2X = query.long2tilex((double)lonAverage, zoom);
            int query2Y = query.lat2tiley((double)minLat, zoom);

            int query4X = query.long2tilex((double)lonAverage, zoom);
            int query4Y = query.lat2tiley((double)latAverage, zoom);

            

            try
            {
                query.DownloadTile(query1X, query1Y, zoom, "tile1.png");
                query.DownloadTile(query2X, query2Y, zoom, "tile2.png");
                query.DownloadTile(query3X, query3Y, zoom, "tile3.png");
                query.DownloadTile(query4X, query4Y, zoom, "tile4.png");

                query.CombineImages("tile1.png", "tile2.png", "tile3.png", "tile4.png");
                return true;
            }

            catch (Exception ex) {
                return false;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            float minLat = float.Parse(textBox1.Text.Replace(".", ","));
            float minLon = float.Parse(textBox2.Text.Replace(".", ","));
            float maxLat = float.Parse(textBox3.Text.Replace(".", ","));
            float maxLon = float.Parse(textBox4.Text.Replace(".", ","));


            int zoom = query.ZoomAmountFinder(minLon, maxLon);
            int queryX = query.long2tilex((double)minLon, zoom);
            int queryY = query.lat2tiley((double)minLat, zoom);
            downloadStatus = false;

            downloadStatus = DownloadHandler(minLat, minLon, maxLat, maxLon);
            //downloadStatus = query.DownloadTile(queryX, queryY, zoom, "tile_original.png");
            isTextureSet = false;


            ShowProgressPanel(true);
            GenerateCoordinateTerrain(minLat, minLon, maxLat, maxLon);

            
            /*Console.WriteLine("Diagonal Distance: " + query.FindDistance(minLat, minLon, maxLat, maxLon).ToString());
            Console.WriteLine("Latitudal Distance: " + query.FindDistance(minLat, minLon, maxLat, minLon).ToString());
            Console.WriteLine("Longitudal Distance: " + query.FindDistance(minLat, minLon, minLat, maxLon).ToString());*/
            ShowProgressPanel(false);


            vertexBuffer.SetData(verts, 0, LockFlags.None);
            indexBuffer.SetData(indices, 0, LockFlags.None);
            /*OnCreateVertexBuffer(vertexBuffer, null);
            OnCreateIndexBuffer(indexBuffer, null);*/

        }

        Vector3 lightPosition = new Vector3(0,2,0);

        bool isAltDown = false;
        private void OnKeyUp(object sender, KeyEventArgs e)
        {


            switch (e.KeyCode)
            {
                case (Keys.Menu):
                    {
                        isAltDown = false;
                        break;
                    }

            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {

            switch (e.KeyCode)
            {

                case (Keys.Menu):
                    {
                        isAltDown = true;
                        break;
                    }
                case (Keys.W):
                    {
                        camPosition.X += camMoveSpeed * (float)Math.Sin(camRotY);
                        camPosition.Z += camMoveSpeed * (float)Math.Cos(camRotY);
                        break;
                    }
                case (Keys.S):
                    {

                        camPosition.X -= camMoveSpeed * (float)Math.Sin(camRotY);
                        camPosition.Z -= camMoveSpeed * (float)Math.Cos(camRotY);
                        break;
                    }
                case (Keys.A):
                    {

                        camPosition.X -= camMoveSpeed * (float)Math.Sin(camRotY + Math.PI / 2);
                        camPosition.Z -= camMoveSpeed * (float)Math.Cos(camRotY + Math.PI / 2);
                        break;
                    }
                case (Keys.D):
                    {

                        camPosition.X += camMoveSpeed * (float)Math.Sin(camRotY + Math.PI / 2);
                        camPosition.Z += camMoveSpeed * (float)Math.Cos(camRotY + Math.PI / 2);
                        break;
                    }
                case (Keys.Z):
                    {
                        camPosition.Y -= camMoveSpeed;
                        break;
                    }
                case (Keys.C):
                    {
                        camPosition.Y += camMoveSpeed;
                        break;
                    }
                case (Keys.Q):
                    {
                        camRotY -= camTurnSpeed;
                        break;
                    }
                case (Keys.E):
                    {
                        camRotY += camTurnSpeed;
                        break;
                    }



                case (Keys.I):
                    {
                        lightPosition.X += camMoveSpeed * (float)Math.Sin(camRotY);
                        lightPosition.Z += camMoveSpeed * (float)Math.Cos(camRotY);
                        break;
                    }
                case (Keys.K):
                    {

                        lightPosition.X -= camMoveSpeed * (float)Math.Sin(camRotY);
                        lightPosition.Z -= camMoveSpeed * (float)Math.Cos(camRotY);
                        break;
                    }
                case (Keys.J):
                    {

                        lightPosition.X -= camMoveSpeed * (float)Math.Sin(camRotY + Math.PI / 2);
                        lightPosition.Z -= camMoveSpeed * (float)Math.Cos(camRotY + Math.PI / 2);
                        break;
                    }
                case (Keys.L):
                    {

                        lightPosition.X += camMoveSpeed * (float)Math.Sin(camRotY + Math.PI / 2);
                        lightPosition.Z += camMoveSpeed * (float)Math.Cos(camRotY + Math.PI / 2);
                        break;
                    }


            }
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case (MouseButtons.Left):
                    {
                        Point mouseDoubleClickPosition = new Point(e.X, e.Y);
                        PickingTriangle(mouseDoubleClickPosition);
                        Console.WriteLine(DateTime.Now.ToString());
                        //Console.WriteLine(query.MakeQuery(39.8f.ToString("F6").Replace(",", "."), 32.8f.ToString("F6").Replace(",", "."), locationData));
                        break;
                    }

            }
        }

        private void OnMouseEnter(object sender, EventArgs e)
        {
            
               this.panel1.Focus();
             
        }

        private void OnMouseScroll(object sender, MouseEventArgs e)
        {

            camPosition.X += e.Delta * 0.002f * (float)Math.Sin(camRotY);
            camPosition.Z += e.Delta * 0.002f * (float)Math.Cos(camRotY);


        }

        bool isLeftMouseDown = false;
        Vector2 mouseLastPosition;

        private void OnMouseDown(object sender, MouseEventArgs e)
        {

            switch (e.Button)
            {
                case (MouseButtons.Left):
                    {
                        mouseLastPosition = new Vector2(e.X, e.Y);
                        isLeftMouseDown = true;
                        break;
                    }

            }


        }
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case (MouseButtons.Left):
                    {
                        isLeftMouseDown = false;
                        break;
                    }

            }

        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isLeftMouseDown && isAltDown)
            {

                camRotY -= (e.X - mouseLastPosition.X) * 0.001f;


                if (camRotX <= Math.PI / 2 && camRotX >= -Math.PI / 2)
                {
                    camRotX -= (e.Y - mouseLastPosition.Y) * 0.001f;
                }

                if (camRotX > Math.PI / 2) { camRotX = (float)Math.PI / 2 - 0.1f; }
                if (camRotX < -Math.PI / 2) { camRotX = (float)-Math.PI / 2 + 0.1f; }

                mouseLastPosition = new Vector2(e.X, e.Y);



            }
        }


        private void PickingTriangle(Point mouseLocation) {

            IntersectInformation hitLocation;

            Vector3 near, far, direction;

            near = new Vector3(mouseLocation.X, mouseLocation.Y, 0);
            far = new Vector3(mouseLocation.X, mouseLocation.Y, 100);

            near.Unproject(device.Viewport,device.Transform.Projection, device.Transform.View, device.Transform.World);
            far.Unproject(device.Viewport, device.Transform.Projection, device.Transform.View, device.Transform.World);

            direction = near - far;

            for (int i = 0; i < indices.Length; i += 3) {
                if (Geometry.IntersectTri(verts[indices[i]].Position, verts[indices[i + 1]].Position, verts[indices[i + 2]].Position, near, direction, out hitLocation)) {
                    /*
                    verts[indices[i]].Color = Color.Red.ToArgb();
                    verts[indices[i+1]].Color = Color.Red.ToArgb();
                    verts[indices[i+2]].Color = Color.Red.ToArgb();
                    */
                    

                    verts[indices[i]].Position += new Vector3(0, 0.05f, 0);
                    verts[indices[i + 1]].Position += new Vector3(0, 0.05f, 0);
                    verts[indices[i + 2]].Position += new Vector3(0, 0.05f, 0);

                    OnCreateVertexBuffer(vertexBuffer, null);
                    OnCreateIndexBuffer(indexBuffer, null);
                }
            }
        }


        private void UpdateBuffers(int verticeCount, int indiceCount) {
            vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionNormalTextured), verticeCount, device, Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionNormalTextured.Format, Pool.Default);
            indexBuffer = new IndexBuffer(typeof(int), indiceCount, device, Usage.WriteOnly, Pool.Default);
        }
        private bool InitializeGraphics()
        {
            try
            {
                presentParams = new PresentParameters();
                presentParams.Windowed = true;
                presentParams.SwapEffect = SwapEffect.Discard;
                presentParams.EnableAutoDepthStencil = true;
                presentParams.AutoDepthStencilFormat = DepthFormat.D16;

                device = new Device(0, DeviceType.Hardware, this.panel1, CreateFlags.SoftwareVertexProcessing, presentParams);


                GenerateVertices();
                GenerateIndices(terrainWidth, terrainLength, indCount);

                vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionNormalTextured), vertCount, device, Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionNormalTextured.Format, Pool.Default);
                OnCreateVertexBuffer(vertexBuffer, null);


                indexBuffer = new IndexBuffer(typeof(int), indCount, device, Usage.WriteOnly, Pool.Default);
                OnCreateIndexBuffer(indexBuffer, null);

                camPosition = new Vector3(2, 10f, -3.5f);
                camLookAt = new Vector3(100, 9f, -2.5f);
                camUp = new Vector3(0, 1, 0);

                return true;

            }
            catch (DirectXException ex)
            {

                Console.WriteLine(ex.ErrorString);
                return false;
            }
        }

        private void InitializeEventHandler()
        {

            vertexBuffer.Created += new EventHandler(this.OnCreateVertexBuffer);
            indexBuffer.Created += new EventHandler(this.OnCreateIndexBuffer);

            this.KeyDown += new KeyEventHandler(OnKeyDown);
            this.KeyUp += new KeyEventHandler(OnKeyUp);
            //this.MouseWheel += new MouseEventHandler(OnMouseScroll);
            this.MouseDown += new MouseEventHandler(OnMouseDown);
            this.MouseUp += new MouseEventHandler(OnMouseUp);
            this.MouseMove += new MouseEventHandler(OnMouseMove);
            this.MouseDoubleClick += new MouseEventHandler(OnMouseDoubleClick);
            
            

            this.panel1.KeyDown += new KeyEventHandler(OnKeyDown);
            this.panel1.KeyUp += new KeyEventHandler(OnKeyUp);
            this.panel1.MouseWheel += new MouseEventHandler(OnMouseScroll);
            this.panel1.MouseDown += new MouseEventHandler(OnMouseDown);
            this.panel1.MouseUp += new MouseEventHandler(OnMouseUp);
            this.panel1.MouseMove += new MouseEventHandler(OnMouseMove);
            this.panel1.MouseDoubleClick += new MouseEventHandler(OnMouseDoubleClick);
            this.panel1.MouseEnter += new EventHandler(OnMouseEnter);



        }

        public void OnCreateIndexBuffer(object sender, EventArgs e)
        {
            IndexBuffer ib = (IndexBuffer)sender;
            ib.SetData(indices, 0, LockFlags.None);
        }

        public void OnCreateVertexBuffer(object sender, EventArgs e)
        {
            VertexBuffer vb = (VertexBuffer)sender;
            vb.SetData(verts, 0, LockFlags.None);
        }



        private void SetupCamera()
        {
            camLookAt.X = (float)Math.Sin(camRotY) + camPosition.X + (float)(Math.Sin(camRotX) * Math.Sin(camRotY));
            camLookAt.Y = (float)Math.Sin(camRotX) + camPosition.Y;
            camLookAt.Z = (float)Math.Cos(camRotY) + camPosition.Z + (float)(Math.Sin(camRotX) * Math.Cos(camRotY));

            device.Transform.Projection = Matrix.PerspectiveFovLH((float)Math.PI * 0.5f, this.panel1.Width / this.panel1.Height, 0.1f, 300f);
            device.Transform.View = Matrix.LookAtLH(camPosition, camLookAt, camUp);
            device.RenderState.CullMode = Cull.None;
            //device.RenderState.Lighting = true;
            device.RenderState.FillMode = fillMode;

        }

        private void SetupLights()
        {
            // Set up a colored directional light, with an oscillating direction.
            // Note that many lights may be active at a time (but each one slows down
            // the rendering of the scene). However, here just one is used.
            device.Lights[0].Type = LightType.Point;
            device.Lights[0].Diffuse = System.Drawing.Color.Red;
            device.Lights[0].Position = lightPosition;
            device.Lights[0].Range = 0f;
            //device.Lights[0].Direction = new Vector3(0,-1,0);
            
            device.Lights[0].Update();
            device.Lights[0].Enabled = true; // Turn it on
            
            
            device.RenderState.Lighting = false;
            device.RenderState.Ambient = System.Drawing.Color.Black;

            
        }


            private void solidToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fillMode = FillMode.Solid;
            solidToolStripMenuItem.Checked = true;
            pointToolStripMenuItem.Checked = false;
            wireframeToolStripMenuItem.Checked = false;
        }

        private void pointToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fillMode = FillMode.Point;
            solidToolStripMenuItem.Checked = false;
            pointToolStripMenuItem.Checked = true;
            wireframeToolStripMenuItem.Checked = false;
        }

        private void wireframeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fillMode = FillMode.WireFrame;
            solidToolStripMenuItem.Checked = false;
            pointToolStripMenuItem.Checked = false;
            wireframeToolStripMenuItem.Checked = true;
        }

        private void backgroundColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            invalidating = false;
            if (colorDialog.ShowDialog(this) == DialogResult.OK) {
                
                backgroundColor = colorDialog.Color;
                
            }
            invalidating = true;
            this.Invalidate();
        }

        private IEnumerable<Control> GetAllControls(Control container)
        {
            List<Control> controlList = new List<Control>();
            foreach (Control c in container.Controls)
            {
                controlList.AddRange(GetAllControls(c));
                
                controlList.Add(c);
            }
            return controlList;
        }

        private void UpdateControls(IEnumerable<Control> controls) {
            foreach (Control c in controls) {
                c.Update();
            }
        }

        Material material = new Material();
        ExtendedMaterial extMaterial = new ExtendedMaterial();
        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            device.Clear(ClearFlags.ZBuffer | ClearFlags.Target, backgroundColor, 100f, 1);
            SetupCamera();
            SetupLights();
            device.BeginScene();


            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
            device.Indices = indexBuffer;
            device.SetStreamSource(0, vertexBuffer, 0);


            drawCall(new Vector3(0, 0, 0), new Vector3(0, 0, 0));

            device.SetTexture(0, terrainTexture);
            device.TextureState[0].ColorOperation = TextureOperation.BlendTextureAlpha;
            device.TextureState[0].ColorArgument1 = TextureArgument.TextureColor;
            device.TextureState[0].ColorArgument2 = TextureArgument.Specular;
            device.TextureState[0].AlphaOperation = TextureOperation.BlendTextureAlpha;
            

 

            if (downloadStatus && !isTextureSet)
            {
                terrainTexture = TextureLoader.FromFile(device, "combined_terrain.png");
                /*extMaterial.TextureFilename = "tile1.jpg";
                Material matToSet = extMaterial.Material3D;
                matToSet.Ambient = matToSet.Diffuse;
                matToSet.Specular = matToSet.Diffuse;
                device.Material = matToSet;
                */

                isTextureSet = true;
            }




            
            device.EndScene();

            device.Present();
            UpdateControls(GetAllControls(this));
            



            if (invalidating)
            {
                this.Invalidate();
                
            }

        }
 
        private void loadHeightMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadHeightMap();
            OnCreateVertexBuffer(vertexBuffer, null);
            OnCreateIndexBuffer(indexBuffer, null);
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GenerateVertices();
            OnCreateVertexBuffer(vertexBuffer, null);
            OnCreateIndexBuffer(indexBuffer, null);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            vertexBuffer.SetData(verts, 0, LockFlags.None);
            indexBuffer.SetData(indices, 0, LockFlags.None);
        }


    
        private void drawCall(Vector3 yaw_pitch_roll, Vector3 translation)
        {


            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertCount, 0, indCount / 3);

        }

    }

}
