using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;


namespace real_time_CI
{
    public class ImageMatrix
    {
        public MainWindow mainWindow;

        // used in seperate detection steps
        public ImageMatrix init_image;
        public ImageMatrix afterConvolve1;
        public ImageMatrix afterConvolve2;
        public ImageMatrix sumConvolves;
        public ImageMatrix afterHough;

        // used in all detection steps at once
        public ImageMatrix allInOne;

        // used in the calcium imaging loop
        public ImageMatrix cycleImage;


        public ImageMatrix(MainWindow a)
        {
            mainWindow = a;
        }

        public int width { get; private set; }  // width of image
        public int height { get; private set; } // height of image
        public float[,] data { get; private set; }  // [horizontal coordinate, vertical coordinate]
        public Bitmap Bitmap { get; private set; } // all pixelvalues in a bitmap
        public int[][][] Borders { get; private set; }  // Borders of the regions. nr borders = nr regions -1
        public int id { get; private set; } // ID for in ImageList
        public double[] intensity { get; set; } // intensity per pixel(group) wrt the last image, per region
        public double[] intensitychange { get; set; } // intensity change per pixel(group) wrt the last image, per region

        public float Max // get maximum value of all pixels in image
        {
            get
            {
                float max = float.MinValue;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (data[x, y] > max)
                        {
                            max = data[x, y];
                        }
                    }
                }
                return max;
            }
        }
        public float Min // get minimum value of all pixels in image
        {
            get
            {
                float min = float.MaxValue;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (data[x, y] < min)
                        {
                            min = data[x, y];
                        }
                    }
                }
                return min;
            }
        }

        public ImageMatrix(Bitmap bmp) // instantiates imagematrix with Bitmap as input
        {
            FillMatrix(bmp);
            CreateBitmap();
        }
        public ImageMatrix(float[,] data) // instantiates imagematrix with all pixeldata as 2d array as input
        {
            width = data.GetLength(0);
            height = data.GetLength(1);
            this.data = data;
            CreateBitmap();
        }

        public ImageMatrix(float[,] data, int[][][] borders) // instantiates imagematrix with all pixeldata, adds border information to imagematrix
        {
            Borders = borders;
            width = data.GetLength(0);
            height = data.GetLength(1);
            this.data = data;
            CreateBitmap();
        }

        private void CreateBitmap() // create a bitmap from 2d array
        {
            Bitmap = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float val = data[x, y];
                    Color c;
                    //we cannot assign pixels with a negative value
                    if (val < 0)
                    {
                        c = Color.Black;
                    }

                    else if (val <= 1 && val >= 0)
                    {
                        int cval = (int)(255 * val);
                        c = Color.FromArgb(cval, cval, cval);//convert back to RGB 0..255
                    }
                    else
                    {
                        c = Color.White;
                    }

                    Bitmap.SetPixel(x, y, c);
                }
            }
        }

        private void FillMatrix(Bitmap bmp) // converts bitmap to 2d array of greyscale data.
        {
            width = bmp.Width;
            height = bmp.Height;
            data = new float[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color c = bmp.GetPixel(x, y);
                    data[x, y] = (c.R + c.G + c.B) / (3f * 255f);//convert RGB to greyscale 0..1

                }
            }
        }

        public ImageMatrix StretchContrast() // brightens image based on initial minimum and maximum brightsness 
        {
            return StretchContrast(Min, Max);
        }

        public ImageMatrix StretchContrast(float min, float max) // brightens image based on initial minimum and maximum brightsness 
        {
            float[,] newdata = new float[width, height];
            float factor = 1f / (max - min);
            if (float.IsInfinity(factor) || float.IsNaN(factor))
            {
                factor = 1f;
            }
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    newdata[x, y] = (data[x, y] - min) * factor;
                }
            }
            return new ImageMatrix(newdata);
        }


        public ImageMatrix ExtendImage(int borderSize) // adds extra pixels at the edge of an image, to improve quality of convolution.
        {
            int newWidth = width + borderSize * 2;
            int newHeight = height + borderSize * 2;
            float[,] newData = new float[newWidth, newHeight];

            //first copy pixels
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    newData[x + borderSize, y + borderSize] = data[x, y];
                }
            }
            //then extend top and bottom
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < borderSize; y++)
                {
                    newData[x + borderSize, y] = data[x, 0];
                    newData[x + borderSize, newHeight - y - 1] = data[x, height - 1];
                }
            }
            //then extend left and right
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < borderSize; x++)
                {
                    newData[x, y + borderSize] = data[0, y];
                    newData[newWidth - x - 1, y + borderSize] = data[width - 1, y];
                }
            }
            //then extend corners
            for (int y = 0; y < borderSize; y++)
            {
                for (int x = 0; x < borderSize; x++)
                {
                    newData[x, y] = data[0, 0];
                    newData[newWidth - x - 1, y] = data[width - 1, 0];
                    newData[newWidth - x - 1, newHeight - y - 1] = data[width - 1, height - 1];
                    newData[x, newHeight - y - 1] = data[0, height - 1];
                }
            }

            return new ImageMatrix(newData);
        }

        public ImageMatrix LoadImageToImageMatrix() // uses directory and filename to get an imagematrix, load it into imagematrix
        {
            try
            {
                // Retrieve the image and put into the ImageMatrix operator
                init_image = new ImageMatrix(new Bitmap(mainWindow.GetFilePath()));
                mainWindow.UpdateMessagePanel("Image Loaded! \r\n");
                return init_image;
            }
            catch (ArgumentException)
            {
                MessageBox.Show("There was an error." +
                    "Check the path to the image file.");
                return null;
            }
        }


        public float[,] ConstructKernel(int KernelWidth, int KernelHeight, float mode) // construct kernel for convolution, mode: 1.0f for light on dark, -1.0f for dark on light
        {
            float[,] Kernel = new float[KernelWidth, KernelHeight];

            for (int x = 0; x < KernelWidth; x++)
            {
                for (int y = 0; y < KernelHeight; y++)
                {
                    if (y != 1)
                    {
                        Kernel[x, y] = -1f * mode;
                    }
                    else if (y == 1)
                    {
                        Kernel[x, y] = 2f * mode;
                    }
                }
            }

            return Kernel;
        }


        public ImageMatrix Convolve(float mode, float LineThreshold) // convolves imagematrix, uses threshold to filter, mode: 1.0f for light on dark, -1.0f for dark on light
        {
            int KernelHeight = 3;
            int KernelWidth = KernelHeight;

            float[,] Kernel = ConstructKernel(KernelWidth, KernelHeight, mode);

            float[,] Image = data;
            float[,] ResultImage = new float[width,height];

            float sum = new float();

            //convolution over every pixel of original image (extendimage is used to cover the pixels of the original outer image)
            for (int i = 1; i < width-1; i++)
            {
                for (int j = 1; j < height-1; j++)
                {
                    for (int k = 0; k < KernelWidth; k++)
                    {
                        for (int l = 0; l < KernelHeight; l++)
                        {
                            sum += Kernel[k, l] * Image[i - 1 + k, j - 1 + l];
                        }
                    }
                    if (sum < LineThreshold)
                    {
                        ResultImage[i - 1, j - 1] = 0f;
                    }
                    else
                    {
                        ResultImage[i - 1, j - 1] = sum;
                    }
                    sum = 0f;
                }
            }
            return new ImageMatrix(ResultImage);
        }


        public ImageMatrix FindChannels(ImageMatrix originalImage, ImageMatrix sumConvolves, int desiredLines) // all steps to realiably find channels in image and project them on original image
        {
            int[] ChannelsLeft = new int[desiredLines];
            int[] ChannelsRight = new int[desiredLines];

            ChannelsLeft  = HoughTransform(originalImage, desiredLines,sumConvolves.data);                     //left side
            sumConvolves.mainWindow.SetProgressBar(70);
            ChannelsRight = HoughTransform(originalImage, desiredLines,VerticalMirror(sumConvolves.data,sumConvolves.width,sumConvolves.height));                //right side
            sumConvolves.mainWindow.SetProgressBar(90);
            originalImage = DrawChannels(originalImage, ChannelsLeft, ChannelsRight);
            originalImage.Borders = GetBorders(originalImage.width, originalImage.height, ChannelsLeft, ChannelsRight, desiredLines);
            sumConvolves.mainWindow.SetProgressBar(100);
            originalImage = DrawBorders(originalImage, desiredLines);

            return originalImage;
        }


        public int[] HoughTransform(ImageMatrix originalImage, int desiredLines, float[,] ImageData) // use houghtransform to detect most probable lines (in form of y = rc_param*x+ verticalpixel) on output of convolution
        {
            float rc_param = 0.005f;         // controls how steep the lines can be. 0.004 is a slope of ~12.5 percent. The max value depends on nr of channels
            float channelwidth_param = 70;  // Controls over how many pixels the mean is taken when extracting the middle of a channel

            double Line = new double();
            int StripLength = (int) (width/2);
            int StripHeigth = height;
            int NrAngles = 20;

            double[] rc = new double[2 * NrAngles + 1];
            double decPart = new double();
            int intPart = new int();

            double[] BrightestAngle = new double[40];
            double[] LineValues = new double[StripHeigth];
            double[] Slope = new double[2 * NrAngles];
            double[] BestSlope = new double[StripHeigth];         // array with 'best' angles for every pixel

            for (int x = 0; x < StripHeigth; x++)           // For every vertical pixel: add the brightness values of 100 horizontal pixels at different angles.
            {
                for (int y = -NrAngles; y < NrAngles; y++)  // For number of angles of lines through pixel 
                {
                    rc[y + NrAngles] = y * rc_param;
                    for (int z = 0; z < width; z++)   // For every horizontal pixel in the line rc*x
                    {
                        decPart = Math.Abs((rc[y + NrAngles] * z)) % 1;             // Decimal part of heigth of pixel in line rc*x 
                        intPart = (int)(rc[y + NrAngles] * z) + x;          // Integer part of heigth of pixel in line rc*x
                        if (intPart >= 0 && intPart < (StripHeigth - 1))  // If pixel is INSIDE of picture (because lines can go out of the picture)
                        {
                            Line += ImageData[z, intPart] * decPart + ImageData[z, intPart + 1] * (1 - decPart); //Sum of brightness values in line rc*x
                        }
                        else                                // If pixel is outside of picture neglect its contribution. (so add brightness 0)
                        {
                            Line = 0f;
                        }
                    }

                    BrightestAngle[y + NrAngles] = Line;      // Array of lines, all a different angle, with their brightness values
                    Line = 0;                               // reset line value
                }

                LineValues[x] = BrightestAngle.Max();       // Array of brightness values of lines, the lightest out of NrAngles.
                BestSlope[x] = rc[Array.IndexOf(BrightestAngle, BrightestAngle.Max())];
                //LineValues[x] = BrightestAngle.Min();     // Array of brightness values of lines, the darkest out of NrAngles.
            }

            double[] LineValuesUnsorted = new double[LineValues.Length];        //store original copy of LineValues to preserve indexes 
            Array.Copy(LineValues, LineValuesUnsorted, LineValues.Length);
            Array.Sort(LineValues);         //sort: first value the smallest
            Array.Reverse(LineValues);      //sort: last value the smallest



            double[] LineValuesMeans = new double[LineValuesUnsorted.Length];   //calculates mean over .. neighbouring pixels per pixel
            int boundary = (int)channelwidth_param / desiredLines; 
            double sum = 0;

            for (int c = 0; c < LineValuesUnsorted.Length; c++)
            {
                try
                {
                    for (int d = -boundary; d < boundary; d++)
                    {
                        sum += LineValuesUnsorted[c + d];
                    }
                }
                catch (System.IndexOutOfRangeException) { }

                LineValuesMeans[c] = sum / (2 * boundary);
                sum = 0;
            }


            double[] lineValuesPart = new double[(int)height / desiredLines];
            double MaxPart = new double();
            int[] ChannelIndex = new int[desiredLines];

            for (int e = 0; e < desiredLines; e++)
            {
                for (int f = 0; f < lineValuesPart.Length; f++)
                {
                    lineValuesPart[f] = LineValuesMeans[e * lineValuesPart.Length + f];
                }
                MaxPart = lineValuesPart.Max();
                ChannelIndex[e] = Array.IndexOf(lineValuesPart, MaxPart) + (e * lineValuesPart.Length);
            }

            return ChannelIndex;
        }

        public float[,] VerticalMirror(float[,] data, int data_w, int data_h) // mirror all data in 2d array about vertical axis
        {
            float[,] FlippedImageData = new float[data_w,data_h];
            for(int i = 1; i< data_w; i++)
            {
                for(int j = 1; j < data_h; j++)
                {
                    FlippedImageData[i, j] = data[data_w - i, j];
                }
            }
            return FlippedImageData;
        }


        public ImageMatrix DrawChannels(ImageMatrix Image, int[] ChannelIndexLeft, int[] ChannelIndexRight) // draws three white lines in the middel of a channel
        {
            float[] difference = new float[ChannelIndexLeft.Length];
            float diffPerPixel = new float();
            int[] pixelShift = new int[Image.width];
            for (int k = 0; k < ChannelIndexLeft.Length; k++)
            {
                difference[k] =  ChannelIndexRight[k]- ChannelIndexLeft[k];
                diffPerPixel = difference[k] / width;
                for (int i = 0; i < Image.width; i++)
                {
                    pixelShift[i] = (int) (diffPerPixel * i);
                    Image.data[i, ChannelIndexLeft[k] + pixelShift[i] - 1] = 1.0f;
                    Image.data[i, ChannelIndexLeft[k] + pixelShift[i]] = 1.0f;
                    Image.data[i, ChannelIndexLeft[k] + pixelShift[i] + 1] = 1.0f;
                }
            }
            return new ImageMatrix(Image.data);
        }

        public ImageMatrix SumImage(ImageMatrix a, ImageMatrix b) // adds pixelvalue of two images together for every pixel
        {
            float[,] dataA = a.data;
            float[,] dataB = b.data;
            float[,] dataC = new float[a.width,a.height];

            if(a.width == b.width && a.height == b.height)  //check if images are correct size
            {
                for (int i = 0; i < a.width; i++)
                {
                    for (int j = 0; j < a.height; j++)
                    {
                        dataC[i, j] = dataA[i, j] + dataB[i, j];
                        if (dataC[i,j] > 255)                  //addition of two of the brightest pixels should not result in errors.
                        {
                            dataC[i, j] = 255;
                        }
                    }
                }
            }
            else
            {
                mainWindow.UpdateMessagePanel("Images not same size! \r\n");
            }

            return new ImageMatrix(dataC);
        }


        // borders:    [amount of borders]   [vertical row]     [coords]
        // example:               9                  54         [54,344]      
        // Image should be image after 'extend' function.

        public int[][][] GetBorders(int width, int height, int[] ChannelIndexLeft, int[] ChannelIndexRight, int NrChannels) //makes array of arrays of border coordinates. 
        {
            int NrBorders = NrChannels - 1;
            int[] BorderIndexesLeft = new int[NrBorders];
            int[] BorderIndexesRight = new int[NrBorders];
            for (int x = 0; x< NrBorders; x++)
            {
                BorderIndexesLeft[x] = (int) (ChannelIndexLeft[x+1] - 0.5*(ChannelIndexLeft[x + 1] - ChannelIndexLeft[x]));
                BorderIndexesRight[x] = (int) (ChannelIndexRight[x+1] - 0.5*(ChannelIndexRight[x + 1] - ChannelIndexRight[x]));
            }
            int[][][] Borders = new int[NrChannels-1][][];

            int[] Difference = new int[NrBorders];
            float DiffPerPixel = new float();
            int[] PixelShift = new int[width];

            for (int y = 0; y< NrBorders; y++)
            {

                Borders[y] = new int[width][]; //declaration jagged array

                Difference[y] = BorderIndexesRight[y] - BorderIndexesLeft[y];
                DiffPerPixel = ((float) Difference[y] / (float) width);
                for(int z = 0; z<width; z++)
                {
                    Borders[y][z] = new int[2];   //declaration of jagged array in loop
                    Borders[y][z] = new int[2];

                    PixelShift[z] = (int)(DiffPerPixel * z);
                    Borders[y][z][1] = BorderIndexesLeft[y] + PixelShift[z];
                    Borders[y][z][0] = z;
                }
            }
            return Borders;
        }


        public ImageMatrix DrawBorders(ImageMatrix Image, int NrChannels) // draw borders in imagematrix
        {
            for(int x = 0; x < NrChannels-1; x++)
            {
                for (int y = 0; y < Image.width; y++)
                {
                    Image.data[Image.Borders[x][y][0], Image.Borders[x][y][1]] = 1.0f;
                }
            }
            return new ImageMatrix(Image.data, Image.Borders);
        }


        public ImageMatrix LoadCycle(string foldername, string filename, int nr) // load image for use in ImageList, using directory and filename. It adds an ID to every image. since later on parts of imagelist will be erased
        {
            cycleImage = new ImageMatrix(new Bitmap(foldername + "\\" + filename));
            cycleImage.id = nr;
            if (allInOne != null)
            {
                cycleImage.Borders = allInOne.Borders; // copy borders from the image after detecting borders. Borders stay the same in the whole experiment (Assumption)
            }
            else
            {
                MessageBox.Show("first detect channels!");
            }
            return cycleImage;
        }

        public double[] GetIntensityChangeGrouped(ImageMatrix CurrentImage, ImageMatrix LastImage, int[][][] borders, double threshold, int groupsize)// get intensity change of two (groups of) pixels of two consecutive images. threshold can be adjusted realtime
        {
            double localintensitycurrent = new double();
            double localintensitycurrentgroup = new double();
            double localintensitylast = new double();
            double localintensitylastgroup = new double();
            double localintensitychange = new double();
            double[] intensitychange = new double[borders.Length + 1];

            // addition first region
            for (int x = 0; x < borders[0].Length - 2 - groupsize; x +=groupsize) 
            {
                for (int y = 0; y < borders[0][x][1]; y += groupsize) 
                {
                    localintensitycurrentgroup = 0;
                    localintensitylastgroup = 0;
                    for(int a = 0; a<groupsize; a++)    // for nr pixels in square of groupsize x groupsize, add up intensities from current frame and add up intensities from last frame.
                    {
                        for (int b = 0; b<groupsize; b++)
                        {
                            localintensitycurrent = CurrentImage.data[x+a, y+b];
                            localintensitylast = LastImage.data[x+a, y+b];
                            localintensitycurrentgroup += localintensitycurrent;
                            localintensitylastgroup += localintensitylast;
                        }
                    }
                    localintensitylastgroup = localintensitylastgroup / (Math.Pow(groupsize,2)); // get mean localintensity of last group
                    localintensitycurrentgroup = localintensitycurrentgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of current group
                    localintensitychange = localintensitycurrentgroup - localintensitylastgroup; // subtract mean local intensity of last group from mean local intensity of current group.

                    if (localintensitychange > threshold)
                    {
                        intensitychange[0] += localintensitychange;
                    }
                }
            }

            // addition in-between regions
            for (int i = 1; i < borders.Length; i++)
            {
                for (int x = 1; x < borders[0].Length - 2 - groupsize; x++)
                {
                    for (int y = borders[i - 1][x][1]; y < borders[i][x][1]; y++)
                    {
                        localintensitycurrentgroup = 0;
                        localintensitylastgroup = 0;
                        for (int a = 0; a < groupsize; a++)    // for nr pixels in square of groupsize x groupsize, add up intensities from current frame and add up intensities from last frame.
                        {
                            for (int b = 0; b < groupsize; b++)
                            {
                                localintensitycurrent = CurrentImage.data[x + a, y + b];
                                localintensitylast = LastImage.data[x + a, y + b];
                                localintensitycurrentgroup += localintensitycurrent;
                                localintensitylastgroup += localintensitylast;
                            }
                        }
                        localintensitylastgroup = localintensitylastgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of last group
                        localintensitycurrentgroup = localintensitycurrentgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of current group
                        localintensitychange = localintensitycurrentgroup - localintensitylastgroup; // subtract mean local intensity of last group from mean local intensity of current group.

                        if (localintensitychange > threshold)
                        {
                            intensitychange[i] += localintensitychange;
                        }
                    }
                }
            }

            // addition last region
            for (int x = 0; x < borders[0].Length - 2 - groupsize; x++)
            {
                for (int y = borders[borders.Length - 1][x][1]; y < CurrentImage.height - groupsize; y++)
                {
                    localintensitycurrentgroup = 0;
                    localintensitylastgroup = 0;
                    for (int a = 0; a < groupsize; a++)    // for nr pixels in square of groupsize x groupsize, add up intensities from current frame and add up intensities from last frame.
                    {
                        for (int b = 0; b < groupsize; b++)
                        {
                            localintensitycurrent = CurrentImage.data[x + a, y + b];
                            localintensitylast = LastImage.data[x + a, y + b];
                            localintensitycurrentgroup += localintensitycurrent;
                            localintensitylastgroup += localintensitylast;
                        }
                    }
                    localintensitylastgroup = localintensitylastgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of last group
                    localintensitycurrentgroup = localintensitycurrentgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of current group
                    localintensitychange = localintensitycurrentgroup - localintensitylastgroup; // subtract mean local intensity of last group from mean local intensity of current group.

                    if (localintensitychange > threshold)
                    {
                        intensitychange[borders.Length] += localintensitychange;
                    }

                }
            }
            
            return intensitychange;
        }

        public double[] GetIntensityGrouped(ImageMatrix CurrentImage, int[][][] borders, double threshold, int groupsize) //get intensity of (groups of) pixel(s) of an image. Threshold can be adjusted realtime
        {
            double localintensitycurrent = new double();
            double localintensitycurrentgroup = new double();
            double localintensity = new double();
            double[] intensity = new double[borders.Length + 1];

            // addition first region
            for (int x = 0; x < borders[0].Length - 2 - groupsize; x += groupsize)
            {
                for (int y = 0; y < borders[0][x][1]; y += groupsize)
                {
                    localintensitycurrentgroup = 0;
                    for (int a = 0; a < groupsize; a++)    // for nr pixels in square of groupsize x groupsize, add up intensities from current frame and add up intensities from last frame.
                    {
                        for (int b = 0; b < groupsize; b++)
                        {
                            localintensitycurrent = CurrentImage.data[x + a, y + b];
                            localintensitycurrentgroup += localintensitycurrent;
                        }
                    }
                    localintensity = localintensitycurrentgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of current group

                    if (localintensity > threshold)
                    {
                        intensity[0] += localintensity;
                    }
                }
            }

            // addition in-between regions
            for (int i = 1; i < borders.Length; i++)
            {
                for (int x = 1; x < borders[0].Length - 2 -groupsize; x++)
                {
                    for (int y = borders[i - 1][x][1]; y < borders[i][x][1]; y++)
                    {
                        localintensitycurrentgroup = 0;
                        for (int a = 0; a < groupsize; a++)    // for nr pixels in square of groupsize x groupsize, add up intensities from current frame and add up intensities from last frame.
                        {
                            for (int b = 0; b < groupsize; b++)
                            {
                                localintensitycurrent = CurrentImage.data[x + a, y + b];
                                localintensitycurrentgroup += localintensitycurrent;
                            }
                        }
                        localintensity = localintensitycurrentgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of last group

                        if (localintensity > threshold)
                        {
                            intensity[i] += localintensity;
                        }
                    }
                }
            }

            // addition last region
            for (int x = 0; x < borders[0].Length - 2 - groupsize; x++)
            {
                for (int y = borders[borders.Length - 1][x][1]; y < CurrentImage.height - groupsize; y++)
                {
                    localintensitycurrentgroup = 0;
                    for (int a = 0; a < groupsize; a++)    // for nr pixels in square of groupsize x groupsize, add up intensities from current frame and add up intensities from last frame.
                    {
                        for (int b = 0; b < groupsize; b++)
                        {
                            localintensitycurrent = CurrentImage.data[x + a, y + b];
                            localintensitycurrentgroup += localintensitycurrent;
                        }
                    }
                    localintensity = localintensitycurrentgroup / (Math.Pow(groupsize, 2)); // get mean localintensity of last group


                    if (localintensity > threshold)
                    {
                        intensity[borders.Length] += localintensity;
                    }

                }
            }
            return intensity;
        }

    }
}
