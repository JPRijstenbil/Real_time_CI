using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Threading;

namespace real_time_CI
{
    public partial class MainWindow : Form
    {

        public ImageMatrix imageMatrix; // image with properties such as bitmap, 2d array data, borders, width, heigth etc.
        public ImageList imageList;     // list of imagematrices, used and updated during cycle
        static SerialPort Arduino1;     // serial connection
        private bool ArduinoReady;      // true if arduinoconnection is established
        private bool Regionsdefined;    // true if regions are defiend
        private bool RunCycle;          // true if cycle is running
        private int CurrentCycle;       // current cycle
        public Thread cycle;            // new threazd to run the cycle in. This keeps main window responsive.

        public MainWindow() // initialize mainwindow
        {
            InitializeComponent();
            setInputPorts();

            imageMatrix = new ImageMatrix(this);
            imageList = new ImageList(this);
        }

        public string GetFilePath() // get filepath of single image from pop up dialog
        {
            string FullFileName = openFileDialog1.FileName;
            if (FullFileName != null)
            {
                return FullFileName;
            }
            else
            {
                MessageBox.Show("There was an error, check the folder or the filename");
                return textBox1.Text;
            }
        }

        public void UpdateFileTextBox(string filepath) // update folder textbox after selecting from pop-up dialog
        {
            this.textBox1.Text = filepath;
        }

        public void UpdateMessagePanel(string message) // adds message to messagepanel
        {

            this.textBox2.Text += message;
        }

        public void ShowBitmap(ImageMatrix showImage) // show bitmap from imagematrix in picturebox 
        {
            pictureBox1.Image = showImage.Bitmap;
        }


        public void clientsize_Change(object sender, EventArgs e) // change size of the picturebox if window is dragged
        {
            pictureBox1.Size = new Size(ClientSize.Width - 115, ClientSize.Height - 40);
        }


        private void textBox2_TextChanged(object sender, EventArgs e) // automatically scroll messagepanel to newest message
        {
            textBox2.SelectionStart = textBox2.Text.Length;
            textBox2.SelectionLength = 0;
            textBox2.ScrollToCaret();
        }

        public bool IsImage() //check if selected file is a compatible image file
        {
            string[] FileExtension = { openFileDialog1.FileName.Substring(openFileDialog1.FileName.Length - 4, 4) }; //openFileDialog1.FileName.Length - 5, openFileDialog1.FileName.Length-1
            string[] AllowedExtensions = { ".tif", ".png", ".jpg", ".gif" };
            bool a = FileExtension.Any(AllowedExtensions.Contains);
            return a;
        }

        public bool IsImage(string filename) //check if selected file is a compatible image file
        {
            if(filename != null && filename != "" && filename.Length > 4)
            {
                string[] FileExtension = { filename.Substring(filename.Length - 4, 4) }; //openFileDialog1.FileName.Length - 5, openFileDialog1.FileName.Length-1
                string[] AllowedExtensions = { ".tif", ".png", ".jpg", ".gif" };
                bool a = FileExtension.Any(AllowedExtensions.Contains);
                return a;
            }
            else
            {
                return false;
            }
        }

        public string GetNewestFile(string foldername) // find newest file in a folder
        {
            try
            {
                var directory = new DirectoryInfo(foldername);
                FileInfo[] a = directory.GetFiles("*.tif");
                FileInfo[] b = directory.GetFiles("*.jpg");
                FileInfo[] c = directory.GetFiles("*.png");

                FileInfo[] d = new FileInfo[a.Length + b.Length + c.Length];
                d = a.Concat(b).ToArray();
                d = d.Concat(c).ToArray();

                var myFile = d.OrderByDescending(f => f.LastAccessTime).First();
                return myFile.Name;
            }
            catch
            {
                return null;
            }
        }

        public void SetProgressBar(int value) // adjust progressbar value
        {
            progressBar1.Value = value;
        }

        #region clickables

        private void button1_Click_1(object sender, EventArgs e) // start connection with arduino
        {
            setupArduino();
            StopActuation(); // prevent actuation when connection is made
        }

        private void button3_Click(object sender, EventArgs e) // stop connection with arduino
        {
            Arduino1.Close();
            ArduinoReady = false;
            button3.Enabled = false; 
            button1.Enabled = true;
        }

        private void comboBox2_Clicked(object sender, EventArgs e) //update all available ports when combobox is unfolded
        {
            setInputPorts();
        }

        private void button2_Click(object sender, EventArgs e) // load file using a pop up file browser
        {
            if (!fileOpened)
            {
                openFileDialog1.InitialDirectory = folderBrowserDialog1.SelectedPath;
                openFileDialog1.FileName = null;
            }

            // Display the openFile dialog.
            DialogResult result = openFileDialog1.ShowDialog();

            // OK button was pressed.
            if (result == DialogResult.OK)
            {
                openFileName = openFileDialog1.FileName;
                closeMenuItem.Enabled = fileOpened;
            }

            // Cancel button was pressed.
            else if (result == DialogResult.Cancel)
            {
                return;
            }

            imageMatrix.LoadImageToImageMatrix();
            ShowBitmap(imageMatrix.init_image);

            pictureBox1.Size = new Size(ClientSize.Width - 105, ClientSize.Height - 40);

            textBox2.Text += "height =" + imageMatrix.init_image.height + "\r\n";
            textBox2.Text += "width = " + imageMatrix.init_image.width + "\r\n";
        }

        private void button4_Click_1(object sender, EventArgs e) // load folder using a pop up folder browser
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                folderName = folderBrowserDialog1.SelectedPath;
                if (!fileOpened)
                {
                    // No file is opened, bring up openFileDialog in selected path.
                    openFileDialog1.InitialDirectory = folderName;
                    openFileDialog1.FileName = null;
                    openMenuItem.PerformClick();
                    UpdateFileTextBox(openFileDialog1.InitialDirectory);
                }
            }
        }

        private void button10_Click(object sender, EventArgs e) // start channel detection
        {
            if (IsImage())
            {
                imageMatrix.init_image = imageMatrix.init_image.StretchContrast();
                SetProgressBar(10);
                imageMatrix.init_image = imageMatrix.init_image.ExtendImage(1);
                SetProgressBar(20);
                imageMatrix.afterConvolve1 = imageMatrix.init_image.Convolve(-1f, 0.35f); // find dark lines on lighter background
                SetProgressBar(30);
                imageMatrix.afterConvolve2 = imageMatrix.init_image.Convolve(1f, 0.8f); // find light lines on darker background
                SetProgressBar(45);
                imageMatrix.sumConvolves = imageMatrix.SumImage(imageMatrix.afterConvolve1, imageMatrix.afterConvolve2);
                SetProgressBar(55);
                int NrLines = Decimal.ToInt32(numericUpDown1.Value);
                if (NrLines >= 1)
                {
                    imageMatrix.sumConvolves.mainWindow = this;
                    imageMatrix.allInOne = imageMatrix.sumConvolves.FindChannels(imageMatrix.init_image, imageMatrix.sumConvolves, NrLines);
                    ShowBitmap(imageMatrix.allInOne);
                    SetProgressBar(0);
                    UpdateMessagePanel("Channels detected and regions defined!");
                    Regionsdefined = true;
                }
                else
                {
                    MessageBox.Show("Cannot find less than ONE channel! Please enter the amount of channels");
                }
            }
            else
            {
                MessageBox.Show("No image loaded. Load image first");
            }

        }

        private void button11_Click(object sender, EventArgs e) //start a cycle in a new thread. this prevents the controls from freezing and the program from not responding during a cycle.
        {
            if (textBox1.Text != "" && ArduinoReady == true && numericUpDown2.Value > 0 && Regionsdefined == true)
            {
                cycle = new Thread(Cycle);
                cycle.Start();

                EnableControls(false);
            }
            else
            {
                MessageBox.Show("Error, cannot start cycle yet. Check all previous steps");
            }
        }


#endregion

        public void EnableControls(bool a) //enable or disable controls during and after the cycle. Some settings should not be adjsuted during a cycle.
        {
            comboBox1.Enabled = a;
            textBox1.Enabled = a;
            textBox3.Enabled = a;
            button2.Enabled = a;
            button10.Enabled = a;
            button4.Enabled = a;
            button11.Enabled = a;
        }

        public void Cycle() // cycle thread
        {
            RunCycle = true;
            CurrentCycle = 0;
            int PreviousCycle = -1;
            int ListOffset = 0;
            
            int NrIterations = decimal.ToInt32(numericUpDown2.Value);
            string directory = textBox1.Text;
            string logfile = textBox3.Text + "_log.txt";
            string randomactuatedchannel = "null";

            int[] actuateLocal = new int[decimal.ToInt32(numericUpDown1.Value)]; // difference between local and non local indexes of channels that should be actuated is that: the picture can contain a subset of the channels in the cellculture, but the app does not know this. 
            int[] actuate = new int[9]; // max nr channels in cell culture

            InitializeLog(directory, logfile); // initialize log

            // search for a new filename in the folder, proceed only if there is a file. This is only for the first image.
            string filename = GetNewestFile(directory);

            while (filename == "" && RunCycle == true && IsImage(filename) == false || filename == null && RunCycle == true && IsImage(filename) == false || IsImage(filename) == false)
            {
                filename = GetNewestFile(directory);
                Thread.Sleep(50);
            }

            if(RunCycle == true)
            {
                imageMatrix.cycleImage = imageMatrix.LoadCycle(directory, filename, 0); 
                imageList.cycleList = imageList.InstantiateImageList(imageMatrix.cycleImage, 0);
            }

            // main while loop
            while (RunCycle == true && CurrentCycle < decimal.ToInt32(numericUpDown2.Value)) // while key esc is not pressed let while loop run. && cycle below desired number of cycles
            {
                // check if new file is added into folder, by comparing curent filename to the new filename. If yes copy this to new filename.
                while (GetNewestFile(directory) == filename && IsImage(filename) == true && RunCycle == true)   //while loop is true if filename has not changed in last 50 ms, AND if filename is image
                {
                    filename = GetNewestFile(directory);
                    Thread.Sleep(50);
                }
                filename = GetNewestFile(directory);

                CurrentCycle += 1;
                PreviousCycle = CurrentCycle - 1;
                
                // load file into imagematrix & imagematrix into imagelist
                imageMatrix.cycleImage = imageMatrix.LoadCycle(directory, filename, CurrentCycle);   // instantiate cycleimage
                imageList.AddToList(imageMatrix.cycleImage, imageList.cycleList, CurrentCycle);

                string txt = "";
                this.Invoke((MethodInvoker)delegate () //invoke is needed because combobox 1 cannot be controlled by another thread than in which it is made. This tells the other thread to change combobox 1
                {
                    txt = comboBox1.Text;
                });

                // perform analysis to get intensity or intensitychange
                if (txt == "Value" && RunCycle == true)
                {
                    imageList.cycleList.Data[CurrentCycle - ListOffset].intensity = imageMatrix.GetIntensityGrouped(imageList.cycleList.Data[CurrentCycle-ListOffset],
                                                                                                        imageList.cycleList.Data[CurrentCycle-ListOffset].Borders,
                                                                                                        Convert.ToDouble(numericUpDown3.Value),
                                                                                                        decimal.ToInt32(numericUpDown4.Value));
                }

                if (txt == "Change" && RunCycle == true) // takes ~200 ms
                {
                    imageList.cycleList.Data[CurrentCycle - ListOffset].intensitychange = imageMatrix.GetIntensityChangeGrouped(imageList.cycleList.Data[CurrentCycle - ListOffset],
                                                                                                                    imageList.cycleList.Data[PreviousCycle - ListOffset],
                                                                                                                    imageList.cycleList.Data[CurrentCycle - ListOffset].Borders,
                                                                                                                    Convert.ToDouble(numericUpDown3.Value),
                                                                                                                    decimal.ToInt32(numericUpDown4.Value));  
                }


                // Set strategy
                if (CurrentCycle > decimal.ToInt32(numericUpDown7.Value)-1)     // actuation based on 5 imagematrices is only possible after 5 photos.
                    {
                    List<double[]> intensityList = new List<double[]>();
                    List<double[]> intensitychangeList = new List<double[]>();

                    for (int i = 0; i < decimal.ToInt32(numericUpDown7.Value); i++)
                    {   
                        if(imageList.cycleList.Data[CurrentCycle - ListOffset].intensity != null)
                        {
                            intensityList.Add(imageList.cycleList.Data[CurrentCycle - ListOffset].intensity);
                        }
                        
                        if(imageList.cycleList.Data[CurrentCycle - ListOffset].intensitychange != null)
                        {
                            intensitychangeList.Add(imageList.cycleList.Data[CurrentCycle - ListOffset].intensitychange);
                        }

                    }

                    if (txt == "Value" && RunCycle == true)
                    {
                        actuateLocal = SetStrategy(intensityList);
                    }

                    if (txt == "Change" && RunCycle == true)
                    {
                        actuateLocal = SetStrategy(intensitychangeList);
                    }

                    // Actuate arduino:
                    Actuate(actuateLocal, decimal.ToInt32(numericUpDown6.Value));


                    // Once in a while actuate a random channel (not instead of a calculated one, just sometimes on random times.)
                    randomactuatedchannel = ActuateRandom();
                    }

                // update log:
                UpdateLog(CurrentCycle,
                            actuateLocal,  // ;
                            imageList.cycleList.Data[CurrentCycle - ListOffset].intensity,
                            imageList.cycleList.Data[CurrentCycle - ListOffset].intensitychange,
                            (float)numericUpDown3.Value,
                            decimal.ToInt32(numericUpDown4.Value),
                            randomactuatedchannel,
                            directory,
                            logfile);

                // show actuated channel on screen:
                if (checkBox2.Checked)
                {
                    ShowBitmap(imageList.cycleList.Data[CurrentCycle - ListOffset].StretchContrast());
                }

                if (checkBox2.Checked == false)
                {
                    ShowBitmap(imageMatrix.allInOne);
                }

                // keep memory low, and keeps only the latest 5 pictures in the list loaded:
                if (CurrentCycle > 5)
                {
                    imageList.RemoveFromList();
                    ListOffset += 1;  
                }
                GC.Collect();

            }
            // stopping cycle

            Actuate(null, 0);

            this.Invoke((MethodInvoker)delegate () // turn on controls after cycle stopped
            {
                bool a = true;
                comboBox1.Enabled = a;
                textBox1.Enabled = a;
                textBox3.Enabled = a;
                button2.Enabled = a;
                button10.Enabled = a;
                button4.Enabled = a;
                button11.Enabled = a;
            });

            imageList.cycleList.Data.Clear();
            imageMatrix.cycleImage = null;

            GC.Collect();

            StopActuation(); 

            MessageBox.Show("Cycle complete!");
            
        }

        private void esc_KeyDown(object sender, KeyEventArgs e)     // while in cycle, check button 'escape' to see if cycle should be stopped.
        {
            if (e.KeyCode == Keys.Escape && cycle.IsAlive)
            {
                RunCycle = false;
                EnableControls(true);
                cycle.Abort();

                GC.Collect(); //garbage collection

                // stop arduino:
                StopActuation();
            }
        }


        public int[] SetStrategy(List<double[]> MeasuredParam)  // Outputs int[] with which channels should be actuated. (with channel numbering as on the image) e.g. channel 1 and 2 on image. 
                                                                // This however can correspond to channel 6 & 7 on the chip. in Actuate() this problem is solved.
        {
            double[] sumMeasuredParam = new double[MeasuredParam[0].Length];
            double a = 0;
            int nrChannelsActuated = Decimal.ToInt32(numericUpDown7.Value);

            int[] ActuatedChannels = new int[nrChannelsActuated];
            int Compare = Decimal.ToInt32(numericUpDown5.Value);

            double MaxMeasuredParam = new double();
            int MaxMeasuredParamIndex = new int();


            for(int i = 0; i< MeasuredParam[0].Length; i++)
            {
                a = 0;
                for(int j = 0; j<MeasuredParam.Count; j++)
                {
                    a += MeasuredParam[j][i];   
                }
                sumMeasuredParam[i] = a;
            }

            for (int i = 0; i < MeasuredParam.Count; i++)
            {
                MaxMeasuredParam = MeasuredParam[i].Max();
                MaxMeasuredParamIndex = MeasuredParam[i].ToList().IndexOf(MeasuredParam[i].Max());    // for every MeasuredParam (intensity or intensitychange) gets index with highest value
            }

            double hi = sumMeasuredParam[0];
            int hiIndex = new int();

            // from sumMeasuredParam we want to get i max numbers -> put in list -> find highest -> remove from list -> find highest etc. 
            for(int i = 0; i< nrChannelsActuated; i++) // for i highest numbers out of sumMeasuredParam:
            {
                hiIndex = 0;
                hi = sumMeasuredParam[0];
                for (int j = 0; j<sumMeasuredParam.Length; j++) // for every number from sumMeasuredParam, compare to get highest and save highest index
                {
                    
                    if (sumMeasuredParam[j] > hi)
                    {
                        hi = sumMeasuredParam[j];
                        hiIndex = j;
                        sumMeasuredParam[j] = 0;
                    }
                }
                ActuatedChannels[i] = hiIndex;
                if (hiIndex == 0)
                {
                    sumMeasuredParam[0] = 0;
                }
            }
            
            return ActuatedChannels;
        }

        public void Actuate(int [] ChannelsToActuate, int FirstChannel) // input ChannelsToActuate is array with lenght #channels and ones for the channels which must be actuated.
        {
            if (ArduinoReady == true)
            {
                byte[] channelByte = { 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // standard no actuation on any channel = zeros;

                if(ChannelsToActuate != null)
                {
                    for (int i = 0; i < ChannelsToActuate.Length; i++)
                    {
                        channelByte[ChannelsToActuate[i] + FirstChannel] = 1;
                    }
                }
                Arduino1.Write(channelByte, 0, 9);
            }

            else
            {
                MessageBox.Show("Error, establish connection with Microcontroller");
            }

        }

        public string ActuateRandom() // randomly actuates one of nine channels
        {
            int c = new int();
            if (checkBox1.Checked)
            {
                Random rnd = new Random();
                int randNr = rnd.Next(0, 10);// chance of actuating random are 1 in 10
                int[] a = { 0 };
                if (randNr == 5)
                {
                    c = rnd.Next(0, 9);
                    Actuate(a, c); // actuate random number between 1 and 9
                    return c.ToString();
                }
                else
                {
                    return "null";
                }
            }
            else
            {
                return "null";
            }

        }


        public void InitializeLog(string foldername, string logname) //make .txt file and write first line with parameter names
        {
            foldername = foldername + "\\" + logname;
            try
            {
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(foldername, true))
                {
                    file.WriteLine("Cycle" + MakeSpaces(5) + "Time" + MakeSpaces(25-4) + "Actuated Channel" + MakeSpaces(4) + "intensity" + MakeSpaces(26) + "intensitychange" + MakeSpaces(20) + "threshold" + MakeSpaces(11) + "pixel groupsize" + MakeSpaces(5) + "Random Actuated Channel");
                }
            }
            catch
            {
                MessageBox.Show("Error, Check folderpath");
                cycle.Abort();
            }
            
        }

        // adds new line to logfile, adds values of measured parameters
        public void UpdateLog(int cycle, int[] actuatedchannel, double[] intensity, double[] intensitychange, float threshold, int pixelgroupsize, string randomactuated, string foldername, string filename)
        {
            intensity = RoundDoubleArray(intensity, 3);
            intensitychange = RoundDoubleArray(intensitychange, 3);

            string Intensity = "";
            string Intensitychange = "";

            for (int i=0; i<actuatedchannel.Length; i++)
            {
                actuatedchannel[i] += decimal.ToInt32(numericUpDown6.Value);
            }

            if (intensity == null)
            {
                Intensity = "NULL";
            }
            else
            {
                Intensity = "{" + string.Join("; ", intensity) + "}";
            }

            if (intensitychange == null)
            {
                Intensitychange = "NULL";
            }
            else
            {
                Intensitychange = "{" + string.Join("; ", intensitychange) + "}";
            }

            string actuatedchannelstring = "{" + string.Join("; ", actuatedchannel) + "}";

            DateTime a = DateTime.Now; // get time

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(foldername + "\\" + filename, true))
            {
                file.WriteLine(cycle.ToString() + MakeSpaces(10 - cycle.ToString().Length)
                                + a.ToString() + MakeSpaces(25 - a.ToString().Length)
                                + actuatedchannelstring + MakeSpaces(20 - actuatedchannelstring.Length)
                                + Intensity + MakeSpaces(35-Intensity.Length)
                                + Intensitychange + MakeSpaces(35-Intensitychange.Length)
                                + threshold.ToString() + MakeSpaces(20-threshold.ToString().Length)
                                + pixelgroupsize + MakeSpaces(19) 
                                + randomactuated) ;
            }
        }

        public string MakeSpaces(int nrspaces) // make array of spaces of length nrspaces
        {
            string b = "";
            for (int i = 0; i<nrspaces; i++)
            {
                b += " ";
            }
            return b;
        }

        public double[] RoundDoubleArray(double[] array, int decimals) // round off doubles to certain number of decimals.
        {
            try
            {
                if (array == null || array.Length == 0)
                {
                    return null;
                }

                else
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = Math.Round(array[i], decimals);
                    }
                    return array;
                }
            }
            catch (NullReferenceException)
            {
                return null;
            }

        }



        private void setInputPorts() // find all occupied USB ports add them to combobox
        {
            string[] allPortNames;
            allPortNames = SerialPort.GetPortNames();
            int nrPorts = allPortNames.Length;

            comboBox2.Items.Clear();
            for (int i = 0; i < nrPorts; i++)
            {
                comboBox2.Items.Add(allPortNames[i]);
            }
        }

        public void StopActuation() // turn all actuation off from all channels
        {
            int[] a = new int[0];
            Actuate(a, 0);
        }


        private void setupArduino() // setup connection with Arduino
        {
            if (ArduinoReady == false)
            {
                try
                {
                    Arduino1 = new SerialPort();
                    Arduino1.PortName = comboBox2.Text;
                    Arduino1.BaudRate = 9600;
                    Arduino1.ReadTimeout = 10000;
                    Arduino1.Open();
                    ArduinoReady = true;
                    textBox2.Text += "\r\n" + "Connection established!";
                    button3.Enabled = true;
                    button1.Enabled = false;
                }
                catch(ArgumentException)
                {
                    MessageBox.Show("Error, check port or physical connection with Arduino");
                }
                catch (System.IO.IOException)
                {
                    MessageBox.Show("Error, time-out period expired. Try a different port");
                }
            }
                
            else
            {
                MessageBox.Show("Error, connection already exists");
            }
        }
    }
}
