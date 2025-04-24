#!/bin/bash

# Create a sine wave using Python
docker exec python-app python -c "
import numpy as np
import matplotlib.pyplot as plt

x = np.linspace(0, 2 * np.pi, 100)
y = np.sin(x)

plt.plot(x, y)
plt.title('Sine Wave')
plt.xlabel('x')
plt.ylabel('sin(x)')
plt.grid(True)
plt.savefig('/usr/src/app/shared/sine_wave.png')
"

# Create a sample UML diagram using PlantUML
docker exec plantuml sh -c "
echo '@startuml
Alice -> Bob: Authentication Request
Bob --> Alice: Authentication Response
Alice -> Bob: Another authentication Request
Bob --> Alice: Another authentication Response
@enduml' > /usr/src/app/shared/sample_diagram.puml
plantuml /usr/src/app/shared/sample_diagram.puml
"

# Output URLs to access the files via nginx
echo "Sine wave image URL: http://localhost/shared/sine_wave.png"
echo "Sample UML diagram URL: http://localhost/shared/sample_diagram.png"