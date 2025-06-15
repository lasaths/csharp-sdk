using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace McpGrasshopperPlugin
{
  public class McpGrasshopperPluginInfo : GH_AssemblyInfo
  {
    public override string Name => "McpGrasshopperPlugin Info";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("dfbcda10-6fa8-4839-a512-8ee41d0bdf5f");

    //Return a string identifying you or your company.
    public override string AuthorName => "";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "";

    //Return a string representing the version.  This returns the same version as the assembly.
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
  }
}
