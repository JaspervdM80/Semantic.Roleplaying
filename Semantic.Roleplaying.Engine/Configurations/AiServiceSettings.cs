using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Semantic.Roleplaying.Engine.Configurations;

public class AIServiceSettings
{
    public AuthenticationSettings Authentication { get; set; } = null!;
    public EndpointSettings Endpoints { get; set; } = null!;
    public ModelSettings Models { get; set; } = null!;
    public VectorSettings VectorSettings { get; set; } = null!;
}
