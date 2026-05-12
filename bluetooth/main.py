"""Project entry point that executes the aggregator component.

This script attempts to import `aggregator` and invoke a
callable entry point: `main()` or `run()`. If neither exists, it will
execute the module as `__main__` so top-level script code runs.

Usage:
  python main.py
"""

import sys
import asyncio
import importlib
import runpy
import inspect

def call_aggregator():
	try:
		mod = importlib.import_module('aggregator')
	except ModuleNotFoundError:
		print("Error: 'aggregator' not found. Ensure 'aggregator.py' exists.")
		raise

	# Check if main() is an async function
	if hasattr(mod, 'main') and callable(mod.main):
		if inspect.iscoroutinefunction(mod.main):
			# main() is async, need to run with asyncio
			return asyncio.run(mod.main())
		else:
			# main() is regular function
			return mod.main()
	
	# Check if run() exists
	if hasattr(mod, 'run') and callable(mod.run):
		if inspect.iscoroutinefunction(mod.run):
			return asyncio.run(mod.run())
		else:
			return mod.run()

	# Fallback: run the module as a script (sets __name__ == '__main__')
	return runpy.run_module('aggregator', run_name='__main__')

if __name__ == '__main__':
	try:
		result = call_aggregator()
		# If the aggregator returns an int, use it as exit code
		if isinstance(result, int):
			sys.exit(result)
	except Exception as e:
		print('Aggregator execution failed:', e)
		sys.exit(1)

