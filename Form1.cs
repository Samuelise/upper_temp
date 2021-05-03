/******filename:Form1.cs
 ******function:智能车上位机 
 ******author: samuelise
 */
using System;
using System.Windows.Forms;
using System.IO.Ports;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;

//自定义命名空间
namespace upper
{

    //定义一个Form1公共类，并创建一个对象
    public partial class Form1 : Form
    {
        public const byte IMAGE_H = 120; //
        public const byte IMAGE_W = 160;
        public const int IMAGE_UART_SIZE = IMAGE_H * IMAGE_W / 8; //传入的图像数据大小
        public const int IMAGE_BUFFER_SIZE = IMAGE_H * IMAGE_W;   //解压后的数据大小

        public const byte NO_IMAGE_MODE = 1;   //不传图像
        public const byte ALL_DATA_MODE = 2;   //传图像和其他数据
        public byte slave_mode = ALL_DATA_MODE;            //数据传输模式（默认为不传图像）

        private List<byte> buffer = new List<byte>(IMAGE_UART_SIZE + 1000);

        //串口控件
        //与类同名的构造方法
        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;

        }
        //自定义方法
        private void Form1_Load(object sender, EventArgs e)
        {
            //批量添加波特率列表
            string[] baud = { "1200", "2400", "9600", "19200", "38400", "115200" };
            //设置默认值
            //下拉列表
            comboBox2.Items.AddRange(baud);
            //获取电脑当前可用串口
            comboBox1.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());

            //按键
            //串口
            button1.Text = "打开串口";
            button1.BackColor = Color.ForestGreen;

            button9.Text = "no image";
        }

        //串口开关
        private void button1_Click(object sender, EventArgs e)
        {
            //可能产生异常的代码放到try中
            try
            {
                if (serialPort1.IsOpen)
                {
                    //串口已经处于打开状态
                    serialPort1.Close();    //关闭串口
                    button1.Text = "打开串口";
                    button1.BackColor = Color.ForestGreen;
                    comboBox1.Enabled = true;
                    comboBox2.Enabled = true;
                }
                else
                {
                    //串口已经处于关闭状态，则设置好串口属性后打开
                    comboBox1.Enabled = false;
                    comboBox2.Enabled = false;

                    serialPort1.PortName = comboBox1.Text;
                    serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);
                    serialPort1.ReceivedBytesThreshold = IMAGE_BUFFER_SIZE;

                    serialPort1.Open();     //打开串口

                    textBox1.AppendText(Convert.ToString(serialPort1.PortName) + "\r\n");
                    textBox1.AppendText(Convert.ToString(serialPort1.BaudRate) + "\r\n");
                    button1.Text = "关闭串口";
                    button1.BackColor = Color.Firebrick;
                }
            }
            catch (Exception ex)
            {
                //捕获可能发生的异常并进行处理

                //捕获到异常，创建一个新的对象，之前的不可以再用
                serialPort1 = new System.IO.Ports.SerialPort();
                //刷新COM口选项
                comboBox1.Items.Clear();
                comboBox1.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
                //响铃并显示异常给用户
                System.Media.SystemSounds.Beep.Play();
                button1.Text = "打开串口";
                button1.BackColor = Color.ForestGreen;
                MessageBox.Show(ex.Message);
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;
            }
        }

        private byte[] imagebuff = new byte[IMAGE_UART_SIZE];  //图像缓存池
        private byte[] image = new byte[IMAGE_H*IMAGE_W];   //解压后的图像
        private int getImage = 0;

        //数据解析
        private void Analyze(byte[] buf)
        {
            switch (slave_mode)
            {
                case ALL_DATA_MODE:
                    break;
                case NO_IMAGE_MODE:
                    break;
                default:
                    textBox1.AppendText("undefined mode!\r\n");
                    break;
            }

        }

        private byte[] ReceiveBytes = new byte[IMAGE_UART_SIZE * 2];
        StringBuilder sb = new StringBuilder();
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int offset = 0;
            int count = serialPort1.BytesToRead;
            textBox1.AppendText("开始接收\r\n");
            byte[] buff = new byte[count];
            serialPort1.Read(buff, 0, count);
            buffer.AddRange(buff); //缓存数据
            //完整性判断
            while (buffer.Count >= 4)
            {
                int len;
                len = IMAGE_UART_SIZE;

                if (buffer.Count < len + 4) break; //等待接收完成

                //得到完整数据
                buffer.CopyTo(0, ReceiveBytes, 0, len + 4);
                buffer.RemoveRange(0, len + 4);//清除数据
                //查找数据头（2位）
                if (ReceiveBytes[0] == 0x00 && ReceiveBytes[1] == 0xff && ReceiveBytes[2] == 0x01 && ReceiveBytes[3] == 0x01)
                {
                    offset += 4;
                    textBox1.AppendText("data dealing.\r\n");
                    Array.Copy(ReceiveBytes, offset, imagebuff, 0, len);
                    image_decompression(imagebuff, image);

                    Bitmap bmp = CreateBitmap(image, IMAGE_W, IMAGE_H);
                    bmp.Save("photo/tmp.bmp");
                    pictureBox1.Image = bmp;
                }
                else
                {
                    buffer.RemoveRange(0, 4);
                    textBox1.AppendText("data format error!");
                }
            }

        }
        void image_decompression(byte[] data1, byte[] data2)
        {
            int i, j;
            int temp;
            int k = 0;
            for (i=0;i< IMAGE_H * (IMAGE_W / 8); i++)
            {
                temp = data1[i];
                for(j=0;j<8;j++)
                {
                    if (Convert.ToBoolean((temp << j) & 0x80)) data2[k] = 255;
                    else data2[k] = 0;
                    k++;
                }
            }
        }

        public static Bitmap CreateBitmap(byte[] originalImageData, int originalWidth, int originalHeight)
        {
            //指定8位格式，即256色
            Bitmap resultBitmap = new Bitmap(originalWidth, originalHeight, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

            //将该位图存入内存中
            MemoryStream curImageStream = new MemoryStream();
            resultBitmap.Save(curImageStream, System.Drawing.Imaging.ImageFormat.Bmp);
            curImageStream.Flush();

            //由于位图数据需要DWORD对齐（4byte倍数），计算需要补位的个数
            int curPadNum = ((originalWidth * 8 + 31) / 32 * 4) - originalWidth;

            //最终生成的位图数据大小
            int bitmapDataSize = ((originalWidth * 8 + 31) / 32 * 4) * originalHeight;

            //数据部分相对文件开始偏移，具体可以参考位图文件格式
            int dataOffset = ReadData(curImageStream, 10, 4);

            //改变调色板，因为默认的调色板是32位彩色的，需要修改为256色的调色板
            int paletteStart = 54;
            int paletteEnd = dataOffset;
            int color = 0;

            for (int i = paletteStart; i < paletteEnd; i += 4)
            {
                byte[] tempColor = new byte[4];
                tempColor[0] = (byte)color;
                tempColor[1] = (byte)color;
                tempColor[2] = (byte)color;
                tempColor[3] = (byte)0;
                color++;

                curImageStream.Position = i;
                curImageStream.Write(tempColor, 0, 4);
            }

            //最终生成的位图数据，以及大小，高度没有变，宽度需要调整
            byte[] destImageData = new byte[bitmapDataSize];
            int destWidth = originalWidth + curPadNum;

            //生成最终的位图数据，注意的是，位图数据 从左到右，从下到上，所以需要颠倒
            for (int originalRowIndex = originalHeight - 1; originalRowIndex >= 0; originalRowIndex--)
            {
                int destRowIndex = originalHeight - originalRowIndex - 1;

                for (int dataIndex = 0; dataIndex < originalWidth; dataIndex++)
                {
                    //同时还要注意，新的位图数据的宽度已经变化destWidth，否则会产生错位
                    destImageData[destRowIndex * destWidth + dataIndex] = originalImageData[originalRowIndex * originalWidth + dataIndex];
                }
            }


            //将流的Position移到数据段   
            curImageStream.Position = dataOffset;

            //将新位图数据写入内存中
            curImageStream.Write(destImageData, 0, bitmapDataSize);

            curImageStream.Flush();

            //将内存中的位图写入Bitmap对象
            resultBitmap = new Bitmap(curImageStream);

            return resultBitmap;
        }

        public static int ReadData(MemoryStream curStream, int startPosition, int length)
        {
            int result = -1;

            byte[] tempData = new byte[length];
            curStream.Position = startPosition;
            curStream.Read(tempData, 0, length);
            result = BitConverter.ToInt32(tempData, 0);

            return result;
        }

        //保存图片：save image
        private void button8_Click(object sender, EventArgs e)
        {
            //test.txt中存保存序号起点
            FileStream fss = new FileStream("photo/test.txt", FileMode.OpenOrCreate);
            fss.Close();
            StreamReader sr = new StreamReader("photo/test.txt", Encoding.Default);
            String line;
            line = sr.ReadLine();
            sr.Close();
            int x = Convert.ToInt16(line);
            x++;
            string filename = @"photo/" + x.ToString() + ".bmp";
            FileInfo fl = new FileInfo(@"photo/tmp.bmp");
            fl.CopyTo(filename);
            FileStream fs = new FileStream("photo/test.txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            //开始写入
            sw.Write(x.ToString());
            //清空缓冲区
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
            textBox1.AppendText("save OK! " + x.ToString() + ".bmp");

        }


        //发送数据使下位机发送图像，并在上位机显示
        private void button9_Click(object sender, EventArgs e)
        {
            if (slave_mode == NO_IMAGE_MODE)
            {
                textBox1.AppendText("show image mode.\r\n");
                slave_mode = ALL_DATA_MODE;
                button9.Text = "no image";

            }
            else if (slave_mode == ALL_DATA_MODE)
            {
                textBox1.AppendText("no image mode.\r\n");
                slave_mode = NO_IMAGE_MODE;
                button9.Text = "show image";
            }
        }
    }
}
