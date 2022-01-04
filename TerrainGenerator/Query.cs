using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;

namespace TerrainGenerator
{
    class Query
    {

        public float[] tileSize = new float[20];
        public int tileSizeIndex = 0;

        public Query() {
            for (int i = 0; i < 20; i++) {
                tileSize[i] = 360 / (float)Math.Pow(2, i);
            }

        }

        public int ZoomAmountFinder(float minLon, float maxLon) {

            float factor = maxLon - minLon;
            float temp = 1;
            int tempIndex = 10;
            for (int i = 0; i < 20; i++)
            {
                if (Math.Abs(tileSize[i] - factor) < temp) {
                    temp = Math.Abs(tileSize[i] - factor);
                    tempIndex = i;
                }
                
            }
            Console.WriteLine("Zoom amount: " + tempIndex);
            return tempIndex;
        }


        public string FindDistance(float minLat, float minLon, float maxLat, float maxLon) {

            //ACOS(COS(RADIANS(90 - Lat1)) * COS(RADIANS(90 - Lat2)) + SIN(RADIANS(90 - Lat1)) * SIN(RADIANS(90 - Lat2)) * COS(RADIANS(Long1 - Long2))) * 6371
            double distance =Math.Acos(Math.Cos(ToRadians(90-maxLat)) * Math.Cos(ToRadians(90 - minLat)) +
                Math.Sin(ToRadians(90 - maxLat)) * Math.Sin(ToRadians(90 - minLat)) * 
                Math.Cos(ToRadians(maxLon - minLon))
                ) *6371 ;
            
            double R = 6371; // metres
            double lat1 = ToRadians(minLat);      //φ1 lat1 * Math.PI / 180; // φ, λ in radians
            double lat2 = ToRadians(maxLat); //φ2 lat2 * Math.PI / 180;
            double latDif = ToRadians(maxLat - minLat);  //Δφ (lat2 - lat1) * Math.PI / 180;
            double lonDif = ToRadians(maxLon - minLon); // Δλ (lon2 - lon1) * Math.PI / 180;

            double a = Math.Sin(latDif / 2) * Math.Sin(latDif / 2) +
                      Math.Cos(lat1) * Math.Cos(lat2) *
                      Math.Sin(lonDif / 2) * Math.Sin(lonDif / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double distance2 = R * c; // in metres
            

            
            //double distance1; 

            return distance + " - " + distance2;

        }



        public double FindEllipticDistance(float minLat, float minLon, float maxLat, float maxLon) {
            double lat1 = ToRadians(minLat);      //φ1 lat1 * Math.PI / 180; // φ, λ in radians
            double lat2 = ToRadians(maxLat); //φ2 lat2 * Math.PI / 180;
            double latDif = ToRadians(maxLat - minLat);  //Δφ (lat2 - lat1) * Math.PI / 180;
            double L = ToRadians(maxLon - minLon); // Δλ (lon2 - lon1) * Math.PI / 180;

            double a = 6378.1370;
            double b = 6356.752314245;
            double f = (a-b)/a;

            double tanU1 = (1 - f) * Math.Tan(lat1);
            double cosU1 = 1 / Math.Sqrt((1 + tanU1 * tanU1));
            double sinU1 = tanU1 * cosU1;

            double tanU2 = (1 - f) * Math.Tan(lat2);
            double cosU2 = 1 / Math.Sqrt((1 + tanU2 * tanU2));
            double sinU2 = tanU2 * cosU2;

            double lambda = L;
            double lambdaTemp = 0;
            int iterationLimit = 100;

            double sinLambda;
            double cosLambda;
            double sinSqTheta;
            double sinTheta;
            double cosTheta;
            double theta;
            double sinAlpha;
            double cosSqAlpha;
            double cos2ThetaM;
            double C;

            do
            {
                sinLambda = Math.Sin(lambda);
                cosLambda = Math.Cos(lambda);
                sinSqTheta = (cosU2 * sinLambda) * (cosU2 * sinLambda) + (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda) * (cosU1 * sinU2 - sinU1 * cosU2 * cosLambda);
                sinTheta = Math.Sqrt(sinSqTheta);
                if (sinTheta == 0) { return 0; }
                cosTheta = sinU1 * sinU2 + cosU1 * cosU2 * cosLambda;
                theta = Math.Atan2(sinTheta, cosTheta);
                sinAlpha = cosU1 * cosU2 * sinLambda / sinTheta;
                cosSqAlpha = 1 - sinAlpha * sinAlpha;
                cos2ThetaM = cosTheta - 2 * sinU1 * sinU2 / cosSqAlpha;
                if (Double.IsNaN(cos2ThetaM)) { cos2ThetaM = 0; } // equatorial line: cosSqα=0 (§6)
                C = f / 16 * cosSqAlpha * (4 + f * (4 - 3 * cosSqAlpha));
                lambdaTemp = lambda;
                lambda = L + (1 - C) * f * sinAlpha * (theta + C * sinTheta * (cos2ThetaM + C * cosTheta * (-1 + 2 * cos2ThetaM * cos2ThetaM)));


            } while (Math.Abs(lambda - lambdaTemp) > 1e-12 && --iterationLimit > 0);
            if (iterationLimit == 0) throw new Exception("Formula failed to converge");

            double uSq = cosSqAlpha * (a * a - b * b) / (b * b);
            double A = 1 + uSq / 16384 * (4096 + uSq * (-768 + uSq * (320 - 175 * uSq)));
            double B = uSq / 1024 * (256 + uSq * (-128 + uSq * (74 - 47 * uSq)));
            double deltaTheta = B * sinTheta * (cos2ThetaM + B / 4 * (cosTheta * (-1 + 2 * cos2ThetaM * cos2ThetaM) -
                B / 6 * cos2ThetaM * (-3 + 4 * sinTheta * sinTheta) * (-3 + 4 * cos2ThetaM * cos2ThetaM)));

            double s = b * A * (theta - deltaTheta);


            return s;
            /*const L = λ2 - λ1;
            const tanU1 = (1 - f) * Math.tan(φ1), cosU1 = 1 / Math.sqrt((1 + tanU1 * tanU1)), sinU1 = tanU1 * cosU1;
            const tanU2 = (1 - f) * Math.tan(φ2), cosU2 = 1 / Math.sqrt((1 + tanU2 * tanU2)), sinU2 = tanU2 * cosU2;

            const λ = L, λʹ, iterationLimit = 100;
            do
            {
                const sinλ = Math.sin(λ), cosλ = Math.cos(λ);
                const sinSqσ = (cosU2 * sinλ) * (cosU2 * sinλ) + (cosU1 * sinU2 - sinU1 * cosU2 * cosλ) * (cosU1 * sinU2 - sinU1 * cosU2 * cosλ);
                const sinσ = Math.sqrt(sinSqσ);
                if (sinσ == 0) return 0;  // co-incident points
                const cosσ = sinU1 * sinU2 + cosU1 * cosU2 * cosλ;
                const σ = Math.atan2(sinσ, cosσ);
                const sinα = cosU1 * cosU2 * sinλ / sinσ;
                const cosSqα = 1 - sinα * sinα;
                const cos2σM = cosσ - 2 * sinU1 * sinU2 / cosSqα;
                if (isNaN(cos2σM)) cos2σM = 0;  // equatorial line: cosSqα=0 (§6)
                const C = f / 16 * cosSqα * (4 + f * (4 - 3 * cosSqα));
                λʹ = λ;
                λ = L + (1 - C) * f * sinα * (σ + C * sinσ * (cos2σM + C * cosσ * (-1 + 2 * cos2σM * cos2σM)));
            } while (Math.abs(λ - λʹ) > 1e-12 && --iterationLimit > 0);
            if (iterationLimit == 0) throw new Error('Formula failed to converge');

            const uSq = cosSqα * (a * a - b * b) / (b * b);
            const A = 1 + uSq / 16384 * (4096 + uSq * (-768 + uSq * (320 - 175 * uSq)));
            const B = uSq / 1024 * (256 + uSq * (-128 + uSq * (74 - 47 * uSq)));
            const Δσ = B * sinσ * (cos2σM + B / 4 * (cosσ * (-1 + 2 * cos2σM * cos2σM) -
                B / 6 * cos2σM * (-3 + 4 * sinσ * sinσ) * (-3 + 4 * cos2σM * cos2σM)));

            const s = b * A * (σ - Δσ);

            const fwdAz = Math.atan2(cosU2 * sinλ, cosU1 * sinU2 - sinU1 * cosU2 * cosλ);
            const revAz = Math.atan2(cosU1 * sinλ, -sinU1 * cosU2 + cosU1 * sinU2 * cosλ);*/

        }


        public double ToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        public int long2tilex(double lon, int z)
        {
            return (int)(Math.Floor((lon + 180.0) / 360.0 * (1 << z)));
        }

        public int lat2tiley(double lat, int z)
        {
            return (int)Math.Floor((1 - Math.Log(Math.Tan(ToRadians(lat)) + 1 / Math.Cos(ToRadians(lat))) / Math.PI) / 2 * (1 << z));
        }

        public double tilex2long(int x, int z)
        {
            return x / (double)(1 << z) * 360.0 - 180;
        }

        public double tiley2lat(int y, int z)
        {
            double n = Math.PI - 2.0 * Math.PI * y / (double)(1 << z);
            return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
        }

        public string MakeQuery(string latQuery, string lonQuery, Dictionary<string, IList<Dictionary<string, string>>> results)
        {

            //var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.30:8090/lookup/?locations^=40,35");

            WebClient wc = new WebClient();

            try
            {
                var url = "http://192.168.1.30:8090/lookup/";
                var jsonData = "{\"locations\":[{\"latitude\":" + latQuery.ToString() + ",\"longitude\":" + lonQuery.ToString() + "}]}";

                using (var client = new WebClient())
                {
                    client.Headers.Add("content-type", "application/json");
                    var response = client.UploadString(url, jsonData);



                    results = JsonConvert.DeserializeObject<Dictionary<string, IList<Dictionary<string, string>>>>(response);
                    //Location stuff = (Location)JObject.Parse(response);
                    string latitude = results["results"][0]["latitude"];
                    string longitude = results["results"][0]["longitude"];
                    string elevation = results["results"][0]["elevation"];
                    return elevation;
                }
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }

        }
        


        public void CombineImages(string fileName1, string fileName2, string fileName3, string fileName4) {

            Bitmap bitmap1 = new Bitmap(fileName1);
            Bitmap bitmap2 = new Bitmap(fileName2);
            Bitmap bitmap3 = new Bitmap(fileName3);
            Bitmap bitmap4 = new Bitmap(fileName4);

            Bitmap bigCanvas = new Bitmap(bitmap1.Width * 2, bitmap1.Height * 2);
            
            for (int i = 0; i < bigCanvas.Width; i++) {
                for (int j = 0; j < bigCanvas.Height; j++) {
                    if (i < bitmap1.Width && j < bitmap1.Height) {
                        bigCanvas.SetPixel(i, j, bitmap3.GetPixel(i, j));
                    }

                    else if (i < bitmap1.Width && j >= bitmap1.Height)
                    {
                        bigCanvas.SetPixel(i, j, bitmap1.GetPixel(i, j - bitmap1.Height)); //
                    }

                    else if (i >= bitmap1.Width && j < bitmap1.Height)
                    {
                        bigCanvas.SetPixel(i, j, bitmap4.GetPixel(i-bitmap1.Width, j)); //
                    }

                    else if (i >= bitmap1.Width && j >= bitmap1.Height)
                    {
                        bigCanvas.SetPixel(i, j, bitmap2.GetPixel(i - bitmap1.Width, j - bitmap1.Height)); //
                    }



                }
            }

            bigCanvas.Save("combined_terrain.png", ImageFormat.Png);
        }

        public bool DownloadTile(int x, int y, int z, string outName)
        {


            WebClient wc = new WebClient();

            try
            {
                string url = "https://b.tiles.mapbox.com/v4/mapbox.satellite/" + z.ToString() + "/" + x.ToString() + "/" + y.ToString() + ".png?access_token=pk.eyJ1IjoibWFwYm94IiwiYSI6ImNpa2lxOXB1eDA0bWl2a2ttc2M3ZmdtdTAifQ.r57Tj9TLieo_bDiN_nL-EA";

                //string url = "https://api.mapbox.com/v4/mapbox.terrain-rgb/" + z.ToString() + "/" + x.ToString() + "/" + y.ToString() + ".pngraw?access_token=pk.eyJ1IjoieWFrdXBoYW4iLCJhIjoiY2pvY3J4YmU5Mmg0azNwcGF2aXByYXQ0eCJ9.JEV_n8EPtk9T8cLSGJJsHA";

                //string url1 = "https://gate.eos.com/api/render/L8/LC80440342015304LGN00/B4,B3,B2/" + z.ToString() + "/" + x.ToString() + "/" + y.ToString() + "?api_key=apk.f31c8aa8a357620f401c8d7a59fa975e08cc7976e3d83236fb400a0fd3bb0ee1";

                string url1 = "https://gate.eos.com/api/render/S1/S1A_IW_GRDH_1SDV_20180803T125259_20180803T125328_023082_02819A_19F1/VV,VH,VV/" + z.ToString() + "/" + x.ToString() + "/" + y.ToString() + "?api_key=apk.f31c8aa8a357620f401c8d7a59fa975e08cc7976e3d83236fb400a0fd3bb0ee1";

                string url2 = "https://api.maptiler.com/tiles/satellite/" + z.ToString() + "/" + x.ToString() + "/" + y.ToString() + "@2x.jpg?key=S1TUlfb5ifo6cUWl9ZAy";

                using (var client = new WebClient())
                {
                    client.DownloadFile(url2, outName);



                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }
    }
}
