import { Button, Divider, IconButton, List, ListItem, ListItemButton, ListItemText, Stack, Tooltip, Typography } from '@mui/material';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutline';
import LogoutIcon from '@mui/icons-material/Logout';
import { Portfolio } from './types';

interface Props {
  items: Portfolio[];
  selectedId?: string | null;
  onSelect: (id: string) => void;
  onCreate: () => void;
  onLogout: () => void;
  mobile?: boolean;
}

export function PortfolioSidebar({ items, selectedId, onSelect, onCreate, onLogout, mobile = false }: Props) {
  return (
    <Stack
      spacing={2}
      sx={{
        width: mobile ? '100%' : 280,
        minHeight: mobile ? '100%' : 'auto',
        p: 2,
      }}
    >
      <Stack direction="row" justifyContent="space-between" alignItems="center">
        <Typography variant="h6" fontWeight={800}>
          Larchik
        </Typography>
        <Tooltip title="Выйти">
          <IconButton size="small" onClick={onLogout}>
            <LogoutIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      <Button
        variant="contained"
        startIcon={<AddCircleOutlineIcon />}
        onClick={onCreate}
        sx={{ textTransform: 'none' }}
        fullWidth
      >
        Новый счет
      </Button>

      <Divider flexItem />

      <Typography variant="overline" color="text.secondary">
        Портфели
      </Typography>
      <List dense disablePadding sx={{ borderRadius: 2, overflow: 'hidden', overflowY: 'auto' }}>
        {items.map((p) => (
          <ListItem key={p.id} disablePadding>
            <ListItemButton selected={p.id === selectedId} onClick={() => onSelect(p.id)}>
              <ListItemText
                primary={p.name}
                primaryTypographyProps={{ fontWeight: 600 }}
                secondary={p.reportingCurrencyId}
                secondaryTypographyProps={{ color: 'text.secondary' }}
              />
            </ListItemButton>
          </ListItem>
        ))}
        {!items.length && (
          <ListItem>
            <ListItemText primary="Нет портфелей" primaryTypographyProps={{ color: 'text.secondary' }} />
          </ListItem>
        )}
      </List>
    </Stack>
  );
}
