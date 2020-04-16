/**
 * A class that represents a spring between two liquid particles
 * Is basically just a class for storing data
 */
public class Spring {

	//The rest length that springs have by default: is set by Main because this is actually a controllable exported quantity
	public static float DefaultRestLength;

	//The current rest-length of the spring: changes over time for plasticity purposes
	public float RestLength = DefaultRestLength;
	
	public LiquidParticle A;

	public LiquidParticle B;

}
