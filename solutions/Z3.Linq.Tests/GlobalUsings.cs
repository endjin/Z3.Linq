global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;

global using Microsoft.VisualStudio.TestTools.UnitTesting;

global using Shouldly;

global using Z3.Linq;

// Z3.Linq.Environment collides with System.Environment, which ImplicitUsings brings in
// globally. Referring to the bare name is CS0104, so use this alias for the Z3.Linq type.
global using Z3Environment = Z3.Linq.Environment;
