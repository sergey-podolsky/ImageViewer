/* * * * * * * * * * * * * * * * * * * * * * * *
 * Simple Image Viewer (Test task)
 *
 * Author:  Sergey Podolsky
 * Mailto:  sergey.podolsky@gmail.com
 * Written:	19.06.2011
 * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using MessageBox = System.Windows.Forms.MessageBox;

namespace ImageViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        /// <summary>
        /// File extensions used by file filter while opening selected folder
        /// </summary>
        private static readonly string[] Extensions = { "bmp", "gif", "jpeg", "jpg", "png", "tiff" };


        /// <summary>
        /// File Browser Dialog imported from standard WinForms library
        /// </summary>
        private readonly FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog { Description = Properties.Resources.MainWindow_folderBrowserDialog_Select_folder_with_images };


        /// <summary>
        /// Show File Browser Dialog.
        /// If the folder is chosen and "OK" button is pressed then get all image files from folder
        /// and show image previews in the ListBox navigation area.
        /// If the folder contains no images then the corresponding message will be shown.
        /// </summary>
        private void ShowLoadFolderDialog(object sender, RoutedEventArgs e)
        {
            // Show FolderBrowserDialog. If the same path is selected then do nothing
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                && !string.Equals(folderBrowserDialog.SelectedPath, folderStatus.Text, StringComparison.OrdinalIgnoreCase))
                try
                {
                    // Change status bar text to selected folder path
                    folderStatus.Text = folderBrowserDialog.SelectedPath;

                    // Clear all images in the navigation area
                    listBox.Items.Clear();

                    // All image files filter expression
                    var files = from filePath in Directory.EnumerateFiles(folderBrowserDialog.SelectedPath)
                                where Extensions.Any(filePath.EndsWith)
                                select filePath;

                    // For each FileInfo that defines image file, load image thumbnail from hard drive and add it to the navigation area
                    Task.Factory.StartNew(() =>
                                              {
                                                  foreach (var filePath in files)
                                                      try
                                                      {
                                                          // Create new bitmap image. Bitmap image is used as ImageSource to reduce
                                                          // load time for preview by DecodePixelWidth property assignment.
                                                          // This approach causes much faster image loading with specified size instead of loading entire image
                                                          var bitmapImage = new BitmapImage();
                                                          bitmapImage.BeginInit();
                                                          // Only Width or Height must be set in order to preserve aspect ratio
                                                          // The default thumbnail preview width is stored in Application Settings
                                                          bitmapImage.DecodePixelWidth = Properties.Settings.Default.ThumbWidth;
                                                          bitmapImage.UriSource = new Uri(filePath);
                                                          bitmapImage.EndInit();

                                                          // Freeze BitmapImage to allow passing it across threads
                                                          bitmapImage.Freeze();

                                                          var path = filePath;
                                                          Dispatcher.BeginInvoke(new Action(delegate()
                                                                                           {
                                                                                               // Create a new StackPanel containing image preview along with a short file name below image
                                                                                               // This may not be set in XAML because of possible exceptions while image file loading
                                                                                               var stackPanel = new StackPanel { Tag = path };
                                                                                               stackPanel.Children.Add(new Image
                                                                                                                           {
                                                                                                                               Source = bitmapImage,
                                                                                                                               Style = (Style)Resources["ThumbnailStyle"]
                                                                                                                           });
                                                                                               stackPanel.Children.Add(new TextBlock
                                                                                                                           {
                                                                                                                               Text = new FileInfo(path).Name,
                                                                                                                               Style = (Style)Resources["TextBlockSyle"]
                                                                                                                           });
                                                                                               // Add created StackPanel to the navigation area ListBox
                                                                                               listBox.Items.Add(stackPanel);
                                                                                           }));
                                                      }
                                                      catch
                                                      {
                                                          // If something went wrong while loading current image then bypass it
                                                      }

                                                  Dispatcher.BeginInvoke(new Action(() =>
                                                                                        {
                                                                                            /* Check if there are any images were enumerated */
                                                                                            if (listBox.HasItems)
                                                                                            {
                                                                                                if (listBox.SelectedItem == null)
                                                                                                    listBox.SelectedItem = listBox.Items[0];
                                                                                            }
                                                                                            else
                                                                                            {
                                                                                                // Show empty image
                                                                                                image.Source = (ImageSource) Resources["bitmapNoImage"];
                                                                                                // Show warning
                                                                                                MessageBox.Show(Properties.Resources.MainWindow_ShowLoadFolderDialog_Folder_contains_no_images,
                                                                                                                Properties.Resources.MainWindow_ShowLoadFolderDialog_Info,
                                                                                                                MessageBoxButtons.OK,
                                                                                                                MessageBoxIcon.Information);
                                                                                            }
                                                                                        }));
                                              });

                }
                catch (IOException exception)
                {
                    System.Windows.MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
        }


        /// <summary>
        /// When user chooses image to display it must be displayed in the main image area
        /// </summary>
        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get StackPanel of ListBox selected item
            var stackPanel = listBox.SelectedItem as StackPanel;
            if (stackPanel == null)
                return;

            // Try to show image thumbnail with low quality in the main image area before the entire image is loaded from hard drive
            try
            {
                // Show low-quality image preview from the navigation area in the main image area
                image.Source = stackPanel.Children.OfType<Image>().First().Source;
            }
            catch (IOException)
            {
                /* If something went wrong while displaying low-quality thumbnail in the main image area then do nothing */
            }

            // Get FileInfo that defines selected image thumbnail in navigation area
            var filePath = stackPanel.Tag as string;
            if (filePath != null)
                try
                {
                    // Load entire image from hard drive in a separate task to prevent window irresponsibility
                    Task.Factory.StartNew(() =>
                                              {
                                                  var bitmapImage = new BitmapImage(new Uri(filePath));
                                                  // Freeze BitmapImage to allow passing it across threads
                                                  bitmapImage.Freeze();
                                                  // Display loaded image in a window dispatcher thread
                                                  Dispatcher.BeginInvoke(new Action(() =>
                                                                                        {
                                                                                            // Show entire high-quality image
                                                                                            image.Source = bitmapImage;
                                                                                            // Display image resolution
                                                                                            imageSize.Text = bitmapImage.PixelWidth + " x " + bitmapImage.PixelHeight;
                                                                                        }));
                                              });
                }
                catch (Exception exception)
                {
                    // If the entire image can not be loaded then show corresponding error message
                    System.Windows.MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
        }


        /// <summary>
        /// Close window on demand
        /// </summary>
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
