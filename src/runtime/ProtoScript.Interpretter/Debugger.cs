namespace ProtoScript.Interpretter
{
	public class Debugger
	{
		public DebuggingInterpretter Interpretter;
		Thread InterpretterThread;
		Compiled.File CompiledFile;
		Compiled.Expression CompiledExpression;
		object oResult = null;

		public Debugger(Compiler compiler)
		{
			Interpretter = new DebuggingInterpretter(compiler);
			InterpretterThread = new Thread(StartInterpretter);
		}

		public void StartDebugging(Compiled.File compiledFile)
		{
			CompiledFile = compiledFile;
			InterpretterThread.Start();
		}
		public void StartDebugging(Compiled.Expression compiledExpression)
		{
			CompiledExpression = compiledExpression;

			//>check if the thread is terminated and create a new one if so
			if (InterpretterThread.ThreadState == ThreadState.Stopped || InterpretterThread.ThreadState == ThreadState.Aborted)
			{
				InterpretterThread = new Thread(StartInterpretter);
			}

			InterpretterThread.Start();
		}

		public void StartInterpretter()
		{
			try
			{
				Interpretter.IsAttached = true;

				if (null != CompiledFile)
				{
					Interpretter.Evaluate(CompiledFile);
				}
				else if (null != CompiledExpression)
				{
					oResult = Interpretter.Evaluate(CompiledExpression);
				}
			}
			catch
			{

			}
		}

		public void Resume()
		{
			Interpretter.Blocked.Release();
		}

		public object WaitForEndOfExecution()
		{
			InterpretterThread.Join();
			Interpretter.IsAttached = false;
			Logs.DebugLog.WriteEvent("Debugger", "Done");
			if (Interpretter.Exception != null)
				throw Interpretter.Exception;

			return oResult;
		}


	}
}
