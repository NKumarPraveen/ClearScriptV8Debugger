using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using V8ChromeDebugEngine;

namespace DebugStub
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string script = "var x=10;\n" +
                            "var y=25;\n" +
                            "var c,d,e,f;\n" +
                            "c=x+y;\n" +
                            "d=x*y;\n" +
                            "e=y/x;\n";
                            //"////winform.System.Windows.Forms.MessageBox.Show(c.toString());\n";
            textBox1.Text = "";
            for (int i = 1; i <= 100; i++)
            {
                await PlayProject(script);
                textBox1.Text = i.ToString();
            }

        }

        private async Task PlayProject(string script)
        {
            try
            {
                using (var engineSession = new V8EngineSession())
                {                    
                    engineSession.BreakpointEvent += async (obj, exxc) =>
                    {
                        try
                        {
                            await engineSession.Continue(V8ChromeDebugEngine.StepAction.In, 1);
                        }
                        catch (Exception)
                        {
                            //throw;
                        }
                    };

                    var bp = new V8ChromeDebugEngine.Breakpoint
                    {
                        LineNumber = 0,
                        Column = null,
                        Enabled = true,
                        Condition = null,
                        IgnoreCount = 0
                    };

                    var res = await engineSession.SetBreakpoint(bp);

                    await engineSession.Evaluate(script);

                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Exception: "+e.Message);
            }
        }
        
    }
}
