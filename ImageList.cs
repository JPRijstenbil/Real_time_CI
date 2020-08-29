using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace real_time_CI
{
    public class ImageList
    {
        public List<ImageMatrix> Data { get; private set; }     // class field contains imagematrixes
        public int ListLength { get; private set; }             // list length

        public ImageList cycleList;                                         
        public MainWindow mainWindow;
        public ImageMatrix cycleImage;

        public ImageList(MainWindow a)  // constructor
        {
            mainWindow = a;
        }

        public ImageList(ImageMatrix imageMatrix, int nr) //constructor
        {
            Data = new List<ImageMatrix>();
            Data.Add(imageMatrix);
            ListLength = Data.Count;
        }

        public ImageList InstantiateImageList(ImageMatrix cycleImage, int nr) // instantiate imagelist
        {
            cycleList = new ImageList(cycleImage, nr);
            return cycleList;
        }

        public ImageList AddToList(ImageMatrix imageMatrix, ImageList List, int nr) // add image to list
        {
            List.Data.Add(imageMatrix);
            List.ListLength = List.Data.Count;
            return List;
        }

        public void RemoveFromList() //removes first instance of the list
        {
            cycleList.Data.Remove(cycleList.Data[0]); 
        }
    }
}
