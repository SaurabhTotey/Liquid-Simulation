/**
 * A class that represents a spring between two liquid particles
 * Is basically just a class for storing data
 */
public class Spring {

	//The current rest-length of the spring: changes over time for plasticity purposes
	public float RestLength;

	public LiquidParticle A;

	public LiquidParticle B;

	/**
	 * Spring constructor: ensures spring is correctly stored in both particles it is attached to
	 */
	public Spring(float restLength, LiquidParticle a, LiquidParticle b) {
		this.RestLength = restLength;
		this.A = a;
		this.B = b;
		a.NeighborToSpring.Add(b, this);
		b.NeighborToSpring.Add(a, this);
	}

	/**
	 * Removes this spring from both the particles it is attached to
	 */
	public void Remove() {
		this.A.NeighborToSpring.Remove(this.B);
		this.B.NeighborToSpring.Remove(this.A);
	}

}
