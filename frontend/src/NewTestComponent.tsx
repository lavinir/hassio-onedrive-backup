import { useEffect } from 'react';
import Button from '@mui/material/Button';

const NewTestComponent = () => {
  useEffect(() => {
    console.log('NewTestComponent mounted');
    const timeoutId = setTimeout(() => {
      alert('NewTestComponent is active!');
    }, 1000);
    
    return () => clearTimeout(timeoutId);
  }, []);

  return (
    <Button 
      variant="contained"
      color="warning"
      onClick={() => alert('NewTestComponent button clicked!')}
    >
      BRAND NEW BUTTON
    </Button>
  );
};

export default NewTestComponent;
