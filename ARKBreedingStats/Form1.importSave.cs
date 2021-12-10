﻿using ARKBreedingStats.miscClasses;
using ARKBreedingStats.settings;
using FluentFTP;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ARKBreedingStats.utils;

namespace ARKBreedingStats
{
    public partial class Form1
    {
        private async void SavegameImportClick(object sender, EventArgs e)
        {
            var error = await RunSavegameImport((ATImportFileLocation)((ToolStripMenuItem)sender).Tag);
            if (string.IsNullOrEmpty(error)) return;
            MessageBoxes.ShowMessageBox(error, "Savegame import error");
        }

        /// <summary>
        /// Imports the creatures from the given saveGame. ftp is possible.
        /// </summary>
        /// <returns>null on success, else an error message to show, or an empty string if the error was already displayed.</returns>
        private async Task<string> RunSavegameImport(ATImportFileLocation atImportFileLocation)
        {
            TsbQuickSaveGameImport.Enabled = false;
            TsbQuickSaveGameImport.BackColor = Color.Yellow;
            ToolStripStatusLabelImport.Text = $"{Loc.S("ImportingSavegame")} {atImportFileLocation.ConvenientName}";
            ToolStripStatusLabelImport.Visible = true;

            try
            {
                string workingCopyFolderPath = Properties.Settings.Default.savegameExtractionPath;
                string workingCopyFilePath;

                // working dir not configured? use temp dir
                // luser configured savegame folder as working dir? use temp dir instead
                if (string.IsNullOrWhiteSpace(workingCopyFolderPath) ||
                    Path.GetDirectoryName(atImportFileLocation.FileLocation) == workingCopyFolderPath)
                {
                    workingCopyFolderPath = Path.GetTempPath();
                }


                if (Uri.TryCreate(atImportFileLocation.FileLocation, UriKind.Absolute, out var uri)
                    && uri.Scheme != "file")
                {
                    switch (uri.Scheme)
                    {
                        case "ftp":
                            workingCopyFilePath = await CopyFtpFileAsync(uri, atImportFileLocation.ConvenientName,
                               workingCopyFolderPath);
                            if (workingCopyFilePath == null)
                                // the user didn't enter credentials
                                return "no credentials";
                            break;
                        default:
                            throw new Exception($"Unsupported uri scheme: {uri.Scheme}");
                    }
                }
                else
                {
                    if (!File.Exists(atImportFileLocation.FileLocation))
                        return $"File not found: {atImportFileLocation.FileLocation}";

                    workingCopyFilePath = Path.Combine(workingCopyFolderPath,
                         Path.GetFileName(atImportFileLocation.FileLocation));
                    try
                    {
                        File.Copy(atImportFileLocation.FileLocation, workingCopyFilePath, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBoxes.ExceptionMessageBox(ex, $"Error while copying the save game file to the working directory\n{workingCopyFolderPath}\nIt's recommended to leave the setting for the working folder empty.");
                        return string.Empty;
                    }
                }

                await ImportSavegame.ImportCollectionFromSavegame(_creatureCollection, workingCopyFilePath,
                    atImportFileLocation.ServerName);

                UpdateParents(_creatureCollection.creatures);

                foreach (var creature in _creatureCollection.creatures)
                {
                    creature.RecalculateAncestorGenerations();
                }

                UpdateIncubationParents(_creatureCollection);

                // update UI
                SetCollectionChanged(true);
                UpdateCreatureListings();

                if (_creatureCollection.creatures.Any())
                    tabControlMain.SelectedTab = tabPageLibrary;

                // reapply last sorting
                listViewLibrary.Sort();

                UpdateTempCreatureDropDown();

                // if unknown mods are used in the savegame-file and the user wants to load the missing mod-files, do it
                if (_creatureCollection.ModValueReloadNeeded
                    && LoadModValuesOfCollection(_creatureCollection, true, true))
                    SetCollectionChanged(true);
            }
            catch (Exception ex)
            {
                MessageBoxes.ExceptionMessageBox(ex, "An error occurred while importing.", "Save file import error");
                return string.Empty;
            }
            finally
            {
                TsbQuickSaveGameImport.Enabled = true;
                TsbQuickSaveGameImport.BackColor = SystemColors.Control;
                ToolStripStatusLabelImport.Visible = false;
            }

            return null; // no error
        }

        private async Task<string> CopyFtpFileAsync(Uri ftpUri, string serverName, string workingCopyFolder)
        {
            var credentialsByServerName = LoadSavedCredentials();
            credentialsByServerName.TryGetValue(serverName, out var credentials);

            var dialogText = $"Ftp Credentials for {serverName}";

            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var progressDialog = new FtpProgressForm(cancellationTokenSource))
            {
                while (true)
                {
                    if (credentials == null)
                    {
                        // get new credentials
                        using (var dialog = new FtpCredentialsForm { Text = dialogText })
                        {
                            if (dialog.ShowDialog(this) == DialogResult.Cancel)
                            {
                                return null;
                            }

                            credentials = dialog.Credentials;

                            if (dialog.SaveCredentials)
                            {
                                credentialsByServerName[serverName] = credentials;
                                Properties.Settings.Default.SavedFtpCredentials = Encryption.Protect(JsonConvert.SerializeObject(credentialsByServerName));
                                Properties.Settings.Default.Save();
                            }
                        }
                    }
                    var client = new FtpClient(ftpUri.Host, ftpUri.Port, credentials.Username, credentials.Password);

                    try
                    {
                        progressDialog.StatusText = $"Authenticating";
                        if (!progressDialog.Visible)
                            progressDialog.Show(this);

                        // TODO
                        // cancel token doesn't work correctly, instead of throwing 
                        // TaskCanceledException
                        // on cancelling it throws
                        // Cannot access a disposed object. Object name: 'System.Net.Sockets.Socket'.
                        await client.ConnectAsync(token: cancellationTokenSource.Token);

                        progressDialog.StatusText = $"Finding most recent file";
                        await Task.Yield();

                        var ftpPath = ftpUri.AbsolutePath;
                        var lastSegment = ftpUri.Segments.Last();
                        if (lastSegment.Contains("*"))
                        {
                            var mostRecentlyModifiedMatch = await GetLastModifiedFileAsync(client, ftpUri, cancellationTokenSource.Token);
                            if (mostRecentlyModifiedMatch == null)
                            {
                                throw new Exception($"No file found matching pattern '{lastSegment}'");
                            }

                            ftpPath = mostRecentlyModifiedMatch.FullName;
                        }

                        var fileName = Path.GetFileName(ftpPath);

                        progressDialog.FileName = fileName;
                        progressDialog.StatusText = $"Downloading {fileName}";
                        await Task.Yield();

                        var filePath = Path.Combine(workingCopyFolder, Path.GetFileName(ftpPath));
                        await client.DownloadFileAsync(filePath, ftpPath, FtpLocalExists.Overwrite, FtpVerify.Retry, progressDialog, token: cancellationTokenSource.Token);
                        await Task.Delay(500, cancellationTokenSource.Token);

                        if (filePath.EndsWith(".gz"))
                        {
                            progressDialog.StatusText = $"Decompressing {fileName}";
                            await Task.Yield();

                            filePath = await DecompressGZippedFileAsync(filePath, cancellationTokenSource.Token);
                        }

                        return filePath;
                    }
                    catch (FtpAuthenticationException ex)
                    {
                        // if auth fails, clear credentials, alert the user and loop until the either auth succeeds or the user cancels
                        progressDialog.StatusText = $"Authentication failed: {ex.Message}";
                        credentials = null;
                        await Task.Delay(1000);
                    }
                    catch (OperationCanceledException)
                    {
                        client?.Dispose();
                        return null;
                    }
                    catch (Exception ex)
                    {
                        if (progressDialog.IsDisposed)
                        {
                            client?.Dispose();
                            return null;
                        }
                        progressDialog.StatusText = $"Unexpected error: {ex.Message}";
                    }
                    finally
                    {
                        client?.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Loads the encrypted ftp crednetials from settings, decrypts them, then returns them as a hostname to credentials dictionary
        /// </summary>
        private static Dictionary<string, FtpCredentials> LoadSavedCredentials()
        {
            try
            {
                var savedCredentials = Encryption.Unprotect(Properties.Settings.Default.SavedFtpCredentials);

                if (!string.IsNullOrEmpty(savedCredentials))
                {
                    var savedDictionary = JsonConvert.DeserializeObject<Dictionary<string, FtpCredentials>>(savedCredentials);

                    // Ensure that the resulting dictionary is case insensitive on hostname
                    return new Dictionary<string, FtpCredentials>(savedDictionary, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                MessageBoxes.ExceptionMessageBox(ex, $"An error occurred while loading saved ftp credentials.");
            }

            return new Dictionary<string, FtpCredentials>(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<string> DecompressGZippedFileAsync(string filePath, CancellationToken cancellationToken)
        {
            var newFileName = filePath.Remove(filePath.Length - 3);

            using (var originalFileStream = File.OpenRead(filePath))
            using (var decompressedFileStream = File.Create(newFileName))
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            {
                await decompressionStream.CopyToAsync(decompressedFileStream, 81920, cancellationToken);
            }

            return newFileName;
        }

        public async Task<FtpListItem> GetLastModifiedFileAsync(FtpClient client, Uri ftpUri, CancellationToken cancellationToken)
        {
            var folderUri = new Uri(ftpUri, ".");
            var listItems = await client.GetListingAsync(folderUri.AbsolutePath, cancellationToken);

            //  Turn the wildcard into a regex pattern   "super*.foo" ->  "^super.*?\.foo$"
            var nameRegex = new Regex("^" + Regex.Escape(ftpUri.Segments.Last()).Replace(@"\*", ".*?") + "$");

            return listItems
                .OrderByDescending(x => x.Modified)
                .FirstOrDefault(x => nameRegex.IsMatch(x.Name));
        }

        /// <summary>
        /// Quick import of selected save games.
        /// </summary>
        private async void TsbQuickSaveGameImport_Click(object sender, EventArgs e)
        {
            var saveImports = Properties.Settings.Default.arkSavegamePaths;
            if (saveImports?.Any() != true)
            {
                if (MessageBox.Show(
                    "No save game files are configured for importing.\nYou can do this in the settings. Do you want to open the according settings-page?",
                    $"Save import not configured - {Utils.ApplicationNameVersion}", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OpenSettingsDialog(Settings.SettingsTabPages.SaveImport);
                return;
            }

            var importLocations = Properties.Settings.Default.arkSavegamePaths
                .Select(ATImportFileLocation.CreateFromString).Where(i => i.ImportWithQuickImport).ToArray();

            if (!importLocations.Any())
            {
                if (MessageBox.Show(
                    "No save game files for the quick import are selected.\nYou can do this in the settings. Do you want to open the according settings-page?",
                    $"Quick import not configured - {Utils.ApplicationNameVersion}", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OpenSettingsDialog(Settings.SettingsTabPages.SaveImport);
                return;
            }

            var results = new List<string>();

            foreach (var importFile in importLocations)
            {
                if (string.IsNullOrEmpty(importFile.FileLocation)
                    || (!(Uri.TryCreate(importFile.FileLocation, UriKind.Absolute, out var uri) && uri.Scheme == "ftp")
                        && !File.Exists(importFile.FileLocation)
                    ))
                {
                    results.Add($"{importFile.ConvenientName}: Error: the file does not exist:\n{importFile.FileLocation}");
                    continue;
                }

                var error = await RunSavegameImport(importFile);

                results.Add(error == null
                        ? $"{importFile.ConvenientName}: Successfully imported."
                        : $"{importFile.ConvenientName}: Error during import:" + (string.IsNullOrEmpty(error) ? string.Empty : $"\n{error}")
                    );
            }

            MessageBoxes.ShowMessageBox(string.Join("\n\n--------\n\n", results), "Save game import done", MessageBoxIcon.Information);
        }
    }
}
