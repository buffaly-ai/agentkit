using BasicUtilities;
using BasicUtilities.Collections;

namespace ProtoScript.Interpretter
{
	public class DebuggingInterpretter : NativeInterpretter
	{
		public List<StatementParsingInfo> Breakpoints = new List<StatementParsingInfo>();
		public Semaphore Blocked = new Semaphore(0, 1);
		public Exception Exception = null;
		public StatementParsingInfo BlockedOn = null;
		public bool BlockOnExceptions = true;

		public bool IsAttached = false;
		public DebuggingInterpretter(Compiler compiler) : base(compiler)
		{

		}

		public enum StepTypes { None,  StepNext, Stop, StepOver };
		public StepTypes Step = StepTypes.None;

		public List<string> CallStack = new List<string>();

		private int? m_iBlockNextCallStackDepth = null;

		public override bool Evaluate(Compiled.Statement statement)
		{
			try
			{
				if (this.Step == StepTypes.Stop)
					return true;

				//Codeblocks
				if (statement.Info == null)
				{
					return base.Evaluate(statement);
				}

				if (this.Step == StepTypes.StepNext)
				{
					Logs.DebugLog.WriteEvent("Debugger", "Stepped in");
					this.Step = StepTypes.None;
					BlockedOn = statement.Info;
					Wait();
				}

				if ((null != m_iBlockNextCallStackDepth && m_iBlockNextCallStackDepth >= CallStack.Count))
				{
					Logs.DebugLog.WriteEvent("Debugger", "Stepped over");
					this.Step = StepTypes.None;
					BlockedOn = statement.Info;
					Wait();
					m_iBlockNextCallStackDepth = null;
				}
				
				foreach (StatementParsingInfo breakpoint in Breakpoints)
				{
					if (ShouldBreak(statement, breakpoint))
					{
						Logs.DebugLog.WriteEvent("Debugger", "Stopped at breakpoint");
						BlockedOn = breakpoint;
						Wait();

					}
				}

				if (this.Step == StepTypes.StepOver)
				{
					m_iBlockNextCallStackDepth = CallStack.Count;
					this.Step = StepTypes.None;
				}

				return base.Evaluate(statement);
			}
			catch (Exception err)
			{
				this.Exception = err;
				if (this.BlockOnExceptions && this.Step != StepTypes.Stop)
				{
					Logs.DebugLog.WriteEvent("Debugger", "Broke on exception");
					BlockedOn = statement.Info;
					Wait();
				}
throw;
			}
		}

		private bool ShouldBreak(Compiled.Statement statement, StatementParsingInfo info)
		{
			if (!StringUtil.EqualNoCase(statement.Info.File, info.File))
				return false;

			return statement.Info.StartingOffset >= info.StartingOffset && info.StoppingOffset >= statement.Info.StartingOffset;

		}

		public override object Evaluate(Compiled.FunctionEvaluation exp)
		{
			CallStack.Add(exp.Function.FunctionName);
			object obj = base.Evaluate(exp);
			CallStack.PopBack();
			return obj;
		}

		public override object Evaluate(Compiled.DotNetMethodEvaluation exp)
		{
			CallStack.Add(exp.Method.Name);
			object obj = base.Evaluate(exp);
			CallStack.PopBack();
			return obj;
		}

		private void Wait()
		{
			//Don't break until the UI is attached
			if (IsAttached)
			{
				Blocked.WaitOne();
				BlockedOn = null;
			}
		}
	}
}
