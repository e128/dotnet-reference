**Review and Refactor Agents & Skills**

1. **Deterministic Shell Scripts**  
   Review all agents and skills. Extract any logic that can be made fully deterministic into new shell scripts. Update the affected agents and skills to call these scripts instead.

2. **Single AI-Focused Purpose**  
   Ensure every skill and agent performs **one clear AI-focused task** exceptionally well. Multiple script calls are allowed, but the AI-specific value (reasoning, decision-making, generation, etc.) must remain singular.  
   Suggest breaking apart any skills/agents that have too much AI variance or try to do multiple distinct things.

3. **Agent Directory Review**  
   Review all agents in `.claude/agents` using the latest Anthropic Claude agent/code guidelines.  
   Identify and address:
    - Overcomplexity
    - Redundancy
    - Obsolescence

4. **Optimization Recommendations**  
   Suggest specific improvements to make each agent:
    - Focused on a single, well-defined task
    - More efficient
    - More autonomous
