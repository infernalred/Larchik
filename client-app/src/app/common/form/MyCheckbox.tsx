import { useField } from 'formik'
import React from 'react'
import { Form, FormCheckbox, Label } from 'semantic-ui-react';

interface Props {
    name: string;
    label?: string;
}

export default function MyCheckbox(props: Props) {
    const [field, meta, helpers] = useField(props.name);

    return (
        <Form.Field error={meta.touched && !!meta.error}>
            <label>{props.label}</label>
            <FormCheckbox
                toggle
                checked={field.value || false}
                onChange={(e, d) => helpers.setValue(d.checked)}
                onBlur={() => helpers.setTouched(true)}
            />
            {meta.touched && meta.error ? (
                <Label basic color='red'>{meta.error}</Label>
            ) : null}
        </Form.Field>
    )
}
