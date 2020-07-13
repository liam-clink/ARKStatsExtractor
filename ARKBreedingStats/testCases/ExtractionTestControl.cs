﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ARKBreedingStats.testCases
{
    public partial class ExtractionTestControl : UserControl
    {
        private ExtractionTestCases cases = new ExtractionTestCases();
        private readonly List<TestCaseControl> extractionTestControls = new List<TestCaseControl>();
        public event TestCaseControl.CopyTestToExtractorEventHandler CopyToExtractor;
        public event TestCaseControl.CopyTestToTesterEventHandler CopyToTester;

        public ExtractionTestControl()
        {
            InitializeComponent();
        }

        public void LoadExtractionTestCases(string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                XmlSerializer reader = new XmlSerializer(typeof(ExtractionTestCases));

                if (!File.Exists(fileName))
                {
                    MessageBox.Show("Save file with name \"" + fileName + "\" does not exist!", $"File not found - {Utils.ApplicationNameVersion}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (System.IO.FileStream file = System.IO.File.OpenRead(fileName))
                {
                    try
                    {
                        cases = (ExtractionTestCases)reader.Deserialize(file);
                        Properties.Settings.Default.LastSaveFileTestCases = fileName;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("File Couldn't be opened, we thought you should know.\nErrormessage:\n\n" + e.Message, $"{Loc.S("error")} - {Utils.ApplicationNameVersion}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        file.Close();
                    }
                }

                ShowTestCases();
                UpdateFileLabel();
            }
        }

        private void SaveExtractionTestCasesToFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                SaveExtractionTestCasesAs();
                return;
            }

            XmlSerializer writer = new XmlSerializer(typeof(ExtractionTestCases));
            try
            {
                System.IO.FileStream file = System.IO.File.Create(fileName);
                writer.Serialize(file, cases);
                file.Close();
                Properties.Settings.Default.LastSaveFileTestCases = fileName;
                UpdateFileLabel();
            }
            catch (Exception e)
            {
                MessageBox.Show("Error during serialization of testcase-data.\nErrormessage:\n\n" + e.Message, $"Serialization-Error - {Utils.ApplicationNameVersion}", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveExtractionTestCasesAs()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "ASB Extraction Testcases (*.json)|*.json";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Properties.Settings.Default.LastSaveFileTestCases = dlg.FileName;
                    SaveTestFile();
                }
            }
        }

        /// <summary>
        /// Display all loaded testcases in controls.
        /// </summary>
        private void ShowTestCases()
        {
            SuspendLayout();
            ClearAll();

            foreach (ExtractionTestCase c in cases.testCases)
            {
                TestCaseControl tcc = new TestCaseControl(c);
                tcc.CopyToExtractor += CopyToExtractor;
                tcc.CopyToTester += CopyToTester;
                tcc.RemoveTestCase += Tcc_RemoveTestCase;
                extractionTestControls.Add(tcc);
                flowLayoutPanelTestCases.Controls.Add(tcc);
                flowLayoutPanelTestCases.SetFlowBreak(tcc, true);
            }
            ResumeLayout();
        }

        private void Tcc_RemoveTestCase(TestCaseControl tcc)
        {
            cases.testCases.Remove(tcc.testCase);
            tcc.Dispose();
            extractionTestControls.Remove(tcc);
            ShowTestCases();
        }

        private void ClearAll(bool clearCases = false)
        {
            foreach (var e in extractionTestControls)
                e.Dispose();
            extractionTestControls.Clear();
            if (cases == null)
                cases = new ExtractionTestCases();
            if (clearCases)
                cases.testCases.Clear();
        }

        /// <summary>
        /// Adds the testcase to the collection.
        /// </summary>
        /// <param name="etc"></param>
        public void AddTestCase(ExtractionTestCase etc)
        {
            cases.testCases.Insert(0, etc);
            ShowTestCases();
        }

        private void newTestfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearAll(true);
            Properties.Settings.Default.LastSaveFileTestCases = string.Empty;
            ShowTestCases();
            UpdateFileLabel();
        }

        private void loadTestfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string initialPath = Application.StartupPath;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastSaveFileTestCases))
                initialPath = Path.GetDirectoryName(Properties.Settings.Default.LastSaveFileTestCases);
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "ASB Extraction Testcases (*.json)|*.json";
                dlg.InitialDirectory = initialPath;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    LoadExtractionTestCases(dlg.FileName);
                }
            }
        }

        private void btSaveTestFile_Click(object sender, EventArgs e)
        {
            SaveTestFile();
        }

        private void saveTestfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveTestFile();
        }

        private void SaveTestFile()
        {
            SaveExtractionTestCasesToFile(Properties.Settings.Default.LastSaveFileTestCases);
        }

        private void saveTestfileAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveExtractionTestCasesAs();
        }

        private void UpdateFileLabel()
        {
            lbTestFile.Text = Properties.Settings.Default.LastSaveFileTestCases;
        }

        private void btRunAllTests_Click(object sender, EventArgs e)
        {
            foreach (var t in extractionTestControls)
                t.ClearTestResult();
            Invalidate();
            foreach (var t in extractionTestControls)
                t.runTest();
        }
    }
}
