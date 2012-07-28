using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Color = System.Windows.Media.Color;

namespace PngDataGen
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged, IDataErrorInfo
    {
        #region Constants

        private const string RGBA_FUNCTION_PATTERN = @"\W*rgba\W*\(([\d\. %]+),([\d. %]+),([\d. %]+),([\d. %]+)\)";

        #endregion

        #region Private fields

        private readonly Regex _rgbaRegex;
        
        private string _rgbaFunctionString;
        private string _generatedCode;
        private bool _rgbaFunctionValueValid;
        private string _rgbaFunctionValueError;

        private System.Drawing.Color _drawingColor;
        private Color _color;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            _rgbaRegex = new Regex(RGBA_FUNCTION_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // set to show error on load
            RgbaFunctionString = string.Empty;

            DataContext = this;
            InitializeComponent();
        }

        #region VM Properties

        /// <summary>
        /// Gets or sets the colour value.
        /// </summary>
        public Color Colour
        {
            get
            {
                return _color;
            }
            set
            {
                if (_color != value)
                {
                    _color = value;
                    RaisePropertyChanged("Colour");
                }
            }
        }


        /// <summary>
        /// Gets or sets the rgba function string.
        /// </summary>
        public string RgbaFunctionString
        {
            get
            {
                return _rgbaFunctionString;
            }
            set
            {
                if (_rgbaFunctionString != value)
                {
                    _rgbaFunctionString = value;
                    IsRgbaFunctionStringValid = (_rgbaFunctionValueError = ParseRgbaFunctionString(value)) == null;

                    RaisePropertyChanged("RgbaFunctionString");
                }
            }
        }

        /// <summary>
        /// Gets or sets the generated code.
        /// </summary>
        public string GeneratedCode
        {
            get
            {
                return _generatedCode;
            }
            set
            {
                if (_generatedCode != value)
                {
                    _generatedCode = value;
                    RaisePropertyChanged("GeneratedCode");
                }
            }
        }


        /// <summary>
        /// Gets or sets a value indicating whether <see cref="RgbaFunctionString"/> is valid.
        /// </summary>
        public bool IsRgbaFunctionStringValid
        {
            get
            {
                return _rgbaFunctionValueValid;
            }
            set
            {
                if (_rgbaFunctionValueValid != value)
                {
                    _rgbaFunctionValueValid = value;
                    RaisePropertyChanged("IsRgbaFunctionStringValid");
                }
            }
        }

        #endregion

        #region Private Methods

        private string ParseRgbaFunctionString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Invalid rgba function format.";

            var match = _rgbaRegex.Match(value);
            if (!match.Success) return "Invalid rgba function format.";

            var channels = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                var str = match.Groups[i + 1].Value;
                if (!TryParseParameter(str, out channels[i])) return string.Format("Invalid parameter '{0}'.", str);
            }

            _drawingColor = System.Drawing.Color.FromArgb(channels[3], channels[0], channels[1], channels[2]);
            Colour = Color.FromArgb(channels[3], channels[0], channels[1], channels[2]);
            return null;
        }

        private bool TryParseParameter(string stringValue, out byte value)
        {
            value = 0;
            bool isPercentage, isDecimal = false;
            stringValue = stringValue.TrimEnd();

            if (isPercentage = stringValue.EndsWith("%"))
            {
                stringValue = stringValue.Substring(0, stringValue.Length - 1);
            }
            else
            {
                isDecimal = stringValue.Contains(".");
            }

            decimal parsedValue;
            if (!decimal.TryParse(stringValue, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsedValue)) return false;

            if (isPercentage) value = (byte)((parsedValue / 100m) * byte.MaxValue);
            else if (isDecimal) value = (byte)(parsedValue * byte.MaxValue);
            else value = (byte)parsedValue;

            return true;
        }

        /// <summary>
        /// Reduces the size of PNG file by removing unneeded chunks.
        /// </summary>
        /// <param name="inStream">The in stream.</param>
        /// <param name="outStream">The out stream.</param>
        /// <remarks>Maximum chunk size of 256 bytes.</remarks>
        private void ReducePng(Stream inStream, Stream outStream)
        {
            var buffer = new byte[256];

            // first read header
            int c = inStream.Read(buffer, 0, 8);
            if (c != 8 || !buffer.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) throw new InvalidOperationException("Stream does not appear to be valid PNG file.");

            // write header
            outStream.Write(buffer, 0, 8);

            // now read/write chunks
            while ((c = inStream.Read(buffer, 0, 4)) == 4)
            {
                var length = BitConverter.IsLittleEndian ? BitConverter.ToInt32(buffer.Take(4).Reverse().ToArray(), 0) : BitConverter.ToInt32(buffer, 0);

                // read rest of chunk
                c += inStream.Read(buffer, c, 8 + length);
                if (c >= 12)
                {
                    switch(Encoding.ASCII.GetString(buffer.Skip(4).Take(4).ToArray()))
                    {
                        case "IHDR":
                        case "IDAT":
                        case "IEND":
                            outStream.Write(buffer, 0, c);
                            break;
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        private void GenerateClick(object sender, RoutedEventArgs e)
        {
            byte[] png;
            using (var image = new Bitmap(1, 1))
            {
                image.SetPixel(0, 0, _drawingColor);

                using (var stream = new MemoryStream())
                {
                    image.Save(stream, ImageFormat.Png);
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var output = new MemoryStream())
                    {
                        ReducePng(stream, output);
                        png = output.ToArray();
                    }
                }
            }

            GeneratedCode = string.Format("url(data:image/png;base64,{0})", Convert.ToBase64String(png));
            generatedCode.Focus();
        }

        #endregion

        #region Implementation of INotifyPropertyChanged

        private void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Implementation of IDataErrorInfo

        /// <inheritdoc />
        public string this[string columnName]
        {
            get
            {
                if (columnName == "RgbaFunctionString")
                {
                    return IsRgbaFunctionStringValid ? null : _rgbaFunctionValueError;
                }

                return null;
            }
        }

        /// <inheritdoc />
        public string Error
        {
            get
            {
                return null;
            }
        }

        #endregion
    }
}
