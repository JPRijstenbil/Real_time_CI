void setup() {
  Serial.begin(9600);
  delay(3000);
  
  pinMode(2, OUTPUT);
  pinMode(3, OUTPUT);
  pinMode(4, OUTPUT);
  pinMode(5, OUTPUT);
  pinMode(6, OUTPUT);
  pinMode(7, OUTPUT);
  pinMode(8, OUTPUT);
  pinMode(9, OUTPUT);
  pinMode(10, OUTPUT);
}

void loop() {
  byte input_byte_buffer[9];
  if (Serial.available() ==9){
    Serial.readBytes(input_byte_buffer,9);
    for(int i = 0; i<9; i++)
    {
      if(input_byte_buffer[i] == 1)
      {
        digitalWrite(i+2,HIGH);
      }
      else
      {
        digitalWrite(i+2,LOW);
      }
    }
  }
} 
